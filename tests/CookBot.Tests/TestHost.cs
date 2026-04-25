using System;
using System.IO;
using CookBot.Application.Recipes;
using CookBot.Application.Services;
using CookBot.Domain.Entities;
using CookBot.Domain.Enums;

namespace CookBot.Tests;

/// <summary>
/// Test bootstrap helpers for Phase 01-04. Provides deterministic factories for the
/// canonical schema stack, the prompt builder, and a stable <see cref="UserProfile"/>
/// fixture used by snapshot tests.
/// </summary>
internal static class TestHost
{
    /// <summary>
    /// Constructs a <see cref="RecipeFormatParser"/> wired to real (in-memory) instances
    /// of the canonical schema stack so tests exercise the same code path that DI resolves.
    /// </summary>
    public static RecipeFormatParser GetParser()
    {
        var chain = new RecipeUpcasterChain(new IRecipeUpcaster[] { new Migration_V1_To_V2() });
        return new RecipeFormatParser(chain, new JsonRecipeSerializer(), new RecipeValidator());
    }

    /// <summary>
    /// Constructs a <see cref="PromptBuilderService"/> backed by the default
    /// <see cref="RecipeSchemaDocumentationProvider"/>.
    /// </summary>
    public static PromptBuilderService GetPromptBuilderService()
    {
        return new PromptBuilderService(new RecipeSchemaDocumentationProvider());
    }

    /// <summary>
    /// Returns a deterministic <see cref="UserProfile"/> fixture for snapshot tests.
    /// Rules (W4 — locked):
    /// - enums use the first-declared value
    /// - non-null string properties carry the property name in lowercase
    /// - ints use 1
    /// - AI-related bools (AiEnabled) use true; other bools use false
    /// - nullable-int navigation fields (AiSharedKeyOwnerUserId) stay null
    /// - User navigation property left null! per the entity convention
    /// </summary>
    public static UserProfile MakeProfile()
    {
        return new UserProfile
        {
            Id = 1,
            UserId = 1,
            ExperienceLevel = ExperienceLevel.Beginner,
            UnitSystem = UnitSystem.Imperial,
            AiUnitExceptions = "aiunitexceptions",
            KitchenToolsJson = "[]",
            DietaryPreferencesJson = "[]",
            AiApiKey = "aiapikey",
            AiSharedKeyOwnerUserId = null,
            AiEnabled = true,
            AiModel = "aimodel",
            AiSystemPromptTemplate = "aisystemprompttemplate",
        };
    }

    /// <summary>
    /// Walks up from <see cref="AppContext.BaseDirectory"/> looking for a sibling
    /// <c>FreelovesCookBot.sln</c> file; returns the directory path. Throws if not found
    /// within 10 levels.
    /// </summary>
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FreelovesCookBot.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate FreelovesCookBot.sln by walking up from AppContext.BaseDirectory.");
    }
}
