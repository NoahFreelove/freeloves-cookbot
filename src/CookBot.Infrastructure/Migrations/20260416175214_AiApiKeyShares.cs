using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AiApiKeyShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AiSharedKeyOwnerUserId",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiApiKeyShares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiApiKeyShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiApiKeyShares_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiApiKeyShares_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_AiSharedKeyOwnerUserId",
                table: "UserProfiles",
                column: "AiSharedKeyOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AiApiKeyShares_OwnerUserId_RecipientUserId",
                table: "AiApiKeyShares",
                columns: new[] { "OwnerUserId", "RecipientUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiApiKeyShares_RecipientUserId",
                table: "AiApiKeyShares",
                column: "RecipientUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_Users_AiSharedKeyOwnerUserId",
                table: "UserProfiles",
                column: "AiSharedKeyOwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_Users_AiSharedKeyOwnerUserId",
                table: "UserProfiles");

            migrationBuilder.DropTable(
                name: "AiApiKeyShares");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_AiSharedKeyOwnerUserId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "AiSharedKeyOwnerUserId",
                table: "UserProfiles");
        }
    }
}
