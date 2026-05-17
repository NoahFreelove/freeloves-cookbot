using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookBot.Infrastructure.Migrations
{
    public partial class SyncRecipeIngredientCompositeIndex : Migration
    {
        // Snapshot-sync only. Phase 8 migration AddPantryMatchIndexes (20260516034227)
        // created IX_RecipeIngredients_RecipeId_IngredientId by hand without updating
        // the fluent config, so the EF model snapshot has been out of sync ever since.
        // Phase 10 / Plan 10-04 added HasIndex(...) to RecipeIngredientConfiguration —
        // this migration's purpose is purely to capture that in the model snapshot.
        // No DB schema change runs here.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
