using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNutritionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CnfFoods",
                columns: table => new
                {
                    FoodId = table.Column<int>(type: "INTEGER", nullable: false),
                    FoodDescription = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    NormalizedDescription = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    FoodGroup = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EnergyKcalPer100g = table.Column<double>(type: "REAL", nullable: false),
                    ProteinGPer100g = table.Column<double>(type: "REAL", nullable: false),
                    FatGPer100g = table.Column<double>(type: "REAL", nullable: false),
                    CarbGPer100g = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CnfFoods", x => x.FoodId);
                });

            migrationBuilder.CreateTable(
                name: "RecipeNutritionCaches",
                columns: table => new
                {
                    RecipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    CanonicalDocHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsStale = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalEnergyKcal = table.Column<double>(type: "REAL", nullable: false),
                    TotalProteinG = table.Column<double>(type: "REAL", nullable: false),
                    TotalFatG = table.Column<double>(type: "REAL", nullable: false),
                    TotalCarbG = table.Column<double>(type: "REAL", nullable: false),
                    Servings = table.Column<int>(type: "INTEGER", nullable: true),
                    PerServingEnergyKcal = table.Column<double>(type: "REAL", nullable: false),
                    PerServingProteinG = table.Column<double>(type: "REAL", nullable: false),
                    PerServingFatG = table.Column<double>(type: "REAL", nullable: false),
                    PerServingCarbG = table.Column<double>(type: "REAL", nullable: false),
                    MatchedIngredients = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalIngredients = table.Column<int>(type: "INTEGER", nullable: false),
                    PerIngredientMatchJson = table.Column<string>(type: "TEXT", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    // WR-04: Concurrency token — Guid refreshed on every write so concurrent
                    // DbContext instances detect stale reads and throw DbUpdateConcurrencyException.
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeNutritionCaches", x => x.RecipeId);
                    table.ForeignKey(
                        name: "FK_RecipeNutritionCaches_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CnfConversionFactors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FoodId = table.Column<int>(type: "INTEGER", nullable: false),
                    MeasureDescription = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ConversionFactorValue = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CnfConversionFactors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CnfConversionFactors_CnfFoods_FoodId",
                        column: x => x.FoodId,
                        principalTable: "CnfFoods",
                        principalColumn: "FoodId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CnfConversionFactors_FoodId",
                table: "CnfConversionFactors",
                column: "FoodId");

            migrationBuilder.CreateIndex(
                name: "IX_CnfFoods_NormalizedDescription",
                table: "CnfFoods",
                column: "NormalizedDescription");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CnfConversionFactors");

            migrationBuilder.DropTable(
                name: "RecipeNutritionCaches");

            migrationBuilder.DropTable(
                name: "CnfFoods");
        }
    }
}
