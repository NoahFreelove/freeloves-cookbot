using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipePhotosTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipePhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    Caption = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    IsPrimary = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipePhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipePhotos_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipePhotos_RecipeId_SortOrder",
                table: "RecipePhotos",
                columns: new[] { "RecipeId", "SortOrder" });

            // GALLERY-01 backfill — one primary RecipePhoto row per existing recipe that has
            // a non-empty PhotoUrl. Runs atomically inside MigrateAsync() so existing single-hero
            // recipes are not disrupted (ROADMAP SC1). Forward-only; no corresponding Down() cleanup
            // needed because Down() drops the whole table.
            migrationBuilder.Sql(@"
                INSERT INTO RecipePhotos (RecipeId, Url, SortOrder, IsPrimary)
                SELECT Id, PhotoUrl, 0, 1
                FROM Recipes
                WHERE PhotoUrl IS NOT NULL AND PhotoUrl != ''
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipePhotos");
        }
    }
}
