using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipePhotoUrlAndDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Recipes",
                type: "TEXT",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "Recipes",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "Recipes");
        }
    }
}
