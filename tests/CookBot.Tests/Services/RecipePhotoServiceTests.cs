using CookBot.Application.DTOs;
using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Interfaces;
using CookBot.Domain.Recipes;
using CookBot.Infrastructure.Data;
using CookBot.Infrastructure.Data.Repositories;
using CookBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CookBot.Tests.Services;

/// <summary>
/// Service-level tests for <see cref="RecipePhotoService"/> proving:
/// - First photo becomes primary and re-syncs Recipe.PhotoUrl
/// - Exactly one IsPrimary after SetPrimary
/// - Promote on primary delete
/// - Cap enforced (MaxPhotosPerRecipe = 2 → 3rd add throws)
/// - Local file deleted on single delete; external URL delete is a no-op
/// - Cross-user mutation throws UnauthorizedAccessException
/// - Reorder reassigns SortOrder
/// </summary>
public class RecipePhotoServiceTests : IDisposable
{
    private readonly CookBotDbContext _db;
    private readonly RecipePhotoService _photoService;
    private readonly RecipeService _recipeService;
    private readonly string _tempUploadsDir;
    private readonly int _userId;
    private readonly int _otherUserId;
    private readonly int _recipeId;

    public RecipePhotoServiceTests()
    {
        // In-memory SQLite fixture (same pattern as RecipeServiceV4FieldsTests)
        var options = new DbContextOptionsBuilder<CookBotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CookBotDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        // Temp uploads dir for file-cleanup tests
        _tempUploadsDir = Path.Combine(Path.GetTempPath(), $"rps-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempUploadsDir);

        var serializer = new JsonRecipeSerializer();

        // Seed owner user + cookbook + recipe
        var user = new User { DisplayName = "Owner" };
        var otherUser = new User { DisplayName = "OtherUser" };
        _db.Users.AddRange(user, otherUser);
        _db.SaveChanges();
        _userId = user.Id;
        _otherUserId = otherUser.Id;

        var cookbook = new Cookbook { UserId = _userId, Name = "Test Cookbook" };
        _db.Cookbooks.Add(cookbook);
        _db.SaveChanges();

        // Seed a minimal canonical recipe so SyncPrimaryPhotoUrlAsync can deserialize
        var canonicalDoc = new RecipeDocument
        {
            Version = RecipeUpcasterChain.CurrentVersion,
            Name = "Test Recipe",
            Servings = 2,
        };
        var recipe = new Recipe
        {
            CookbookId = cookbook.Id,
            Name = "Test Recipe",
            Servings = 2,
            CanonicalDocumentJson = serializer.Serialize(canonicalDoc),
        };
        _db.Recipes.Add(recipe);
        _db.SaveChanges();
        _recipeId = recipe.Id;

        // Build RecipeService with a null photo-file storage (not needed for sync tests)
        var recipeRepo = new Repository<Recipe>(_db);
        var ingredientRepo = new Repository<Ingredient>(_db);
        var cookbookRepo = new Repository<Cookbook>(_db);
        var recipeTagRepo = new Repository<RecipeTag>(_db);
        var recipePhotoRepo = new Repository<RecipePhoto>(_db);

        _recipeService = new RecipeService(
            new StubRecipeFormatParser(),
            recipeRepo,
            ingredientRepo,
            cookbookRepo,
            recipeTagRepo,
            recipePhotoRepo,
            new NullPhotoFileStorage(),
            serializer,
            NullLogger<RecipeService>.Instance);

        // Build a FakePhotoStorage pointing at the temp dir (used by the photo service)
        var fakeStorage = new FakeTempPhotoStorage(_tempUploadsDir);

        var settingsOptions = Options.Create(new CookBotSettings { MaxPhotosPerRecipe = 10 });

        var urlValidator = new RecipePhotoUrlValidator();

        _photoService = new RecipePhotoService(
            _db,
            cookbookRepo,
            _recipeService,
            fakeStorage,
            urlValidator,
            settingsOptions,
            NullLogger<RecipePhotoService>.Instance);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddPhotoAsync_FirstPhoto_BecomesPrimaryAndSyncsPhotoUrl()
    {
        var photo = await _photoService.AddPhotoAsync(_recipeId, "/uploads/test.jpg", _userId);

        Assert.True(photo.IsPrimary);

        var reloaded = await _db.Recipes.FindAsync(_recipeId);
        Assert.Equal("/uploads/test.jpg", reloaded!.PhotoUrl);
    }

    [Fact]
    public async Task SetPrimaryAsync_ExactlyOnePrimaryAfterSwitch()
    {
        // Add 3 photos (first becomes primary)
        await _photoService.AddPhotoAsync(_recipeId, "/uploads/a.jpg", _userId);
        var p2 = await _photoService.AddPhotoAsync(_recipeId, "/uploads/b.jpg", _userId);
        await _photoService.AddPhotoAsync(_recipeId, "/uploads/c.jpg", _userId);

        // Set 2nd as primary
        await _photoService.SetPrimaryAsync(_recipeId, p2.Id, _userId);

        var photos = await _db.RecipePhotos.Where(p => p.RecipeId == _recipeId).ToListAsync();

        var primaryPhotos = photos.Where(p => p.IsPrimary).ToList();
        Assert.Single(primaryPhotos);
        Assert.Equal(p2.Id, primaryPhotos[0].Id);

        var reloaded = await _db.Recipes.FindAsync(_recipeId);
        Assert.Equal("/uploads/b.jpg", reloaded!.PhotoUrl);
    }

    [Fact]
    public async Task DeleteAsync_PrimaryDeleted_PromotesLowestSortOrderAndSyncs()
    {
        // Add 3 photos — first is primary
        var p1 = await _photoService.AddPhotoAsync(_recipeId, "/uploads/first.jpg", _userId);
        var p2 = await _photoService.AddPhotoAsync(_recipeId, "/uploads/second.jpg", _userId);
        var p3 = await _photoService.AddPhotoAsync(_recipeId, "/uploads/third.jpg", _userId);

        Assert.True(p1.IsPrimary);

        // Delete the primary
        await _photoService.DeleteAsync(_recipeId, p1.Id, _userId);

        var remaining = await _db.RecipePhotos
            .Where(p => p.RecipeId == _recipeId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        Assert.Equal(2, remaining.Count);

        var primaryPhotos = remaining.Where(p => p.IsPrimary).ToList();
        Assert.Single(primaryPhotos);
        // Lowest SortOrder remaining should be promoted
        var lowestSort = remaining.OrderBy(p => p.SortOrder).First();
        Assert.Equal(lowestSort.Id, primaryPhotos[0].Id);

        var recipe = await _db.Recipes.FindAsync(_recipeId);
        Assert.Equal(lowestSort.Url, recipe!.PhotoUrl);
    }

    [Fact]
    public async Task AddPhotoAsync_ExceedsCap_ThrowsInvalidOperationException()
    {
        // Override settings with cap = 2
        var cookbookRepo = new Repository<Cookbook>(_db);
        var cappedSettings = Options.Create(new CookBotSettings { MaxPhotosPerRecipe = 2 });
        var cappedService = new RecipePhotoService(
            _db, cookbookRepo, _recipeService,
            new NullPhotoFileStorage(), new RecipePhotoUrlValidator(), cappedSettings,
            NullLogger<RecipePhotoService>.Instance);

        await cappedService.AddPhotoAsync(_recipeId, "/uploads/one.jpg", _userId);
        await cappedService.AddPhotoAsync(_recipeId, "/uploads/two.jpg", _userId);

        // 3rd add must throw
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cappedService.AddPhotoAsync(_recipeId, "/uploads/three.jpg", _userId));
        Assert.Contains("Maximum 2 photos", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_LocalFile_DeletesPhysicalFile()
    {
        // Seed a real file in the temp uploads dir
        var fileName = "real-photo.jpg";
        var fullPath = Path.Combine(_tempUploadsDir, fileName);
        await File.WriteAllBytesAsync(fullPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // fake JPEG bytes
        Assert.True(File.Exists(fullPath));

        // Add the photo with a /uploads/ URL pointing at the real file
        var cookbookRepo = new Repository<Cookbook>(_db);
        var fakeStorage = new FakeTempPhotoStorage(_tempUploadsDir);
        var settings = Options.Create(new CookBotSettings { MaxPhotosPerRecipe = 10 });
        var serviceWithRealStorage = new RecipePhotoService(
            _db, cookbookRepo, _recipeService, fakeStorage, new RecipePhotoUrlValidator(), settings,
            NullLogger<RecipePhotoService>.Instance);

        var photo = await serviceWithRealStorage.AddPhotoAsync(_recipeId, $"/uploads/{fileName}", _userId);

        // Delete the photo
        await serviceWithRealStorage.DeleteAsync(_recipeId, photo.Id, _userId);

        // File should be gone
        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task DeleteAsync_ExternalUrl_NoFileError()
    {
        // Add a photo with an external https URL (no local file)
        var photo = await _photoService.AddPhotoAsync(
            _recipeId, "https://example.com/photo.jpg", _userId);

        // Delete should not throw even though no local file exists
        var exception = await Record.ExceptionAsync(
            () => _photoService.DeleteAsync(_recipeId, photo.Id, _userId));
        Assert.Null(exception);
    }

    [Fact]
    public async Task AddPhotoAsync_CrossUser_ThrowsUnauthorizedAccessException()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _photoService.AddPhotoAsync(_recipeId, "/uploads/x.jpg", _otherUserId));
    }

    [Fact]
    public async Task ReorderAsync_ReassignsSortOrder()
    {
        var p1 = await _photoService.AddPhotoAsync(_recipeId, "/uploads/a.jpg", _userId);
        var p2 = await _photoService.AddPhotoAsync(_recipeId, "/uploads/b.jpg", _userId);
        var p3 = await _photoService.AddPhotoAsync(_recipeId, "/uploads/c.jpg", _userId);

        // Reverse the order
        await _photoService.ReorderAsync(_recipeId, new[] { p3.Id, p2.Id, p1.Id }, _userId);

        var photos = await _photoService.GetPhotosAsync(_recipeId, _userId);
        Assert.Equal(p3.Id, photos[0].Id);
        Assert.Equal(p2.Id, photos[1].Id);
        Assert.Equal(p1.Id, photos[2].Id);

        // SortOrder values should be reassigned
        Assert.Equal(0, photos[0].SortOrder);
        Assert.Equal(1, photos[1].SortOrder);
        Assert.Equal(2, photos[2].SortOrder);
    }

    // ── WR-02: server-side URL validation in AddPhotoAsync ────────────────────

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,abc")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/photo.jpg")]
    public async Task AddPhotoAsync_DisallowedScheme_ThrowsInvalidOperationException(string badUrl)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _photoService.AddPhotoAsync(_recipeId, badUrl, _userId));
        Assert.Contains("Only http and https photo URLs are allowed.", ex.Message);
    }

    [Fact]
    public async Task AddPhotoAsync_UrlExceedsMaxLength_ThrowsInvalidOperationException()
    {
        var tooLong = "https://example.com/" + new string('a', 2048);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _photoService.AddPhotoAsync(_recipeId, tooLong, _userId));
        Assert.Contains("maximum allowed length", ex.Message);
    }

    [Fact]
    public async Task AddPhotoAsync_LocalUploadsPath_BypassesSchemeValidation()
    {
        // /uploads/ paths from the upload pipeline must be accepted without scheme validation
        var photo = await _photoService.AddPhotoAsync(_recipeId, "/uploads/test.jpg", _userId);
        Assert.Equal("/uploads/test.jpg", photo.Url);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_tempUploadsDir, recursive: true); } catch { /* ignore */ }
    }

    // ── Stubs / helpers ───────────────────────────────────────────────────────

    private class StubRecipeFormatParser : IRecipeFormatParser
    {
        public ParsedRecipe Parse(string rawContent) => new();
        public string Serialize(ParsedRecipe recipe) => "";
        public bool TryParse(string rawContent, out ParsedRecipe? recipe, out List<string> errors)
        {
            recipe = new ParsedRecipe();
            errors = new List<string>();
            return true;
        }
    }

    /// <summary>No-op IRecipePhotoFileStorage — used where file I/O is not under test.</summary>
    private class NullPhotoFileStorage : IRecipePhotoFileStorage
    {
        public void DeletePhysicalFile(string url) { }
    }

    /// <summary>
    /// File storage implementation that uses a real temp directory.
    /// Implements <see cref="IRecipePhotoFileStorage"/> by delegating to a temp dir
    /// rather than wwwroot/uploads so tests don't need IWebHostEnvironment.
    /// </summary>
    private sealed class FakeTempPhotoStorage : IRecipePhotoFileStorage
    {
        private readonly string _uploadsDir;

        public FakeTempPhotoStorage(string uploadsDir)
        {
            _uploadsDir = uploadsDir;
        }

        public void DeletePhysicalFile(string url)
        {
            var fileName = Path.GetFileName(url);
            var fullPath = Path.Combine(_uploadsDir, fileName);

            // Only delete files inside the temp dir (mirrors AssertPathInsideUploadsDirectory behavior)
            var resolvedPath = Path.GetFullPath(fullPath);
            var resolvedDir = Path.GetFullPath(_uploadsDir) + Path.DirectorySeparatorChar;
            if (!resolvedPath.StartsWith(resolvedDir, StringComparison.Ordinal))
                throw new InvalidOperationException("Path traversal attempt in test.");

            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
    }
}
