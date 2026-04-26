using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AiConversationFormatVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D-22: back-fill existing rows to FormatVersion = 1 (pre-Phase-2 YAML wire).
            // The entity-side default of 2 only applies to NEW inserts; existing rows need
            // an explicit DB-side defaultValue at column-add time. This is the one allowed
            // manual edit to a generated migration body for this plan.
            migrationBuilder.AddColumn<int>(
                name: "FormatVersion",
                table: "AiConversations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormatVersion",
                table: "AiConversations");
        }
    }
}
