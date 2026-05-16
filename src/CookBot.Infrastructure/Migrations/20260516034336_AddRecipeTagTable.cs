using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeTagTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeTags_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeTags_RecipeId_Name",
                table: "RecipeTags",
                columns: new[] { "RecipeId", "Name" },
                unique: true);

            // Backfill existing tag data from TagsJson column (D-26 / D-34).
            // TRIM enforces D-34 whitespace trimming. WHERE filters empty entries.
            // ON CONFLICT DO NOTHING ensures idempotency (safe to re-run).
            // SQLite BINARY collation keeps "Vegan"/"vegan" as two distinct rows per D-34.
            // TagsJson column is NOT dropped here — Plan 11 owns the drop migration.
            migrationBuilder.Sql(@"
                INSERT INTO RecipeTags (RecipeId, Name)
                SELECT r.Id, TRIM(json_each.value)
                FROM Recipes r, json_each(r.TagsJson)
                WHERE TRIM(json_each.value) <> ''
                ON CONFLICT DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeTags");
        }
    }
}
