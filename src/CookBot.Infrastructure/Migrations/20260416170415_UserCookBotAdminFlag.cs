using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserCookBotAdminFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCookBotAdmin",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("DELETE FROM Users WHERE DisplayName = 'CookBot Admin';");

            migrationBuilder.Sql("""
                UPDATE Users SET IsCookBotAdmin = 1
                WHERE Id = (SELECT MIN(Id) FROM Users WHERE DisplayName = 'Home Chef');
                """);

            migrationBuilder.Sql("""
                UPDATE Users SET IsCookBotAdmin = 1
                WHERE Id = (SELECT MIN(Id) FROM Users)
                AND NOT EXISTS (SELECT 1 FROM Users WHERE IsCookBotAdmin = 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCookBotAdmin",
                table: "Users");
        }
    }
}
