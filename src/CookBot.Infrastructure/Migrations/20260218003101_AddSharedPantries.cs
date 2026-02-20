using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CookBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedPantries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PantryItems_Users_UserId",
                table: "PantryItems");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "PantryItems",
                newName: "PantryId");

            migrationBuilder.RenameIndex(
                name: "IX_PantryItems_UserId_IngredientId",
                table: "PantryItems",
                newName: "IX_PantryItems_PantryId_IngredientId");

            migrationBuilder.CreateTable(
                name: "Pantries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPersonal = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pantries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pantries_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PantryMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PantryId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PantryMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PantryMembers_Pantries_PantryId",
                        column: x => x.PantryId,
                        principalTable: "Pantries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PantryMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pantries_OwnerId",
                table: "Pantries",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PantryMembers_PantryId_UserId",
                table: "PantryMembers",
                columns: new[] { "PantryId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PantryMembers_UserId",
                table: "PantryMembers",
                column: "UserId");

            // Data migration: create a personal pantry for each existing user
            migrationBuilder.Sql(@"
                INSERT INTO Pantries (Name, OwnerId, IsPersonal, CreatedAt)
                SELECT 'Personal Pantry', Id, 1, datetime('now')
                FROM Users;
            ");

            // Data migration: update PantryItems to point to the new personal pantry
            // (PantryId column currently holds the old UserId values)
            migrationBuilder.Sql(@"
                UPDATE PantryItems
                SET PantryId = (
                    SELECT p.Id FROM Pantries p
                    WHERE p.OwnerId = PantryItems.PantryId AND p.IsPersonal = 1
                );
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_PantryItems_Pantries_PantryId",
                table: "PantryItems",
                column: "PantryId",
                principalTable: "Pantries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PantryItems_Pantries_PantryId",
                table: "PantryItems");

            migrationBuilder.DropTable(
                name: "PantryMembers");

            migrationBuilder.DropTable(
                name: "Pantries");

            migrationBuilder.RenameColumn(
                name: "PantryId",
                table: "PantryItems",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_PantryItems_PantryId_IngredientId",
                table: "PantryItems",
                newName: "IX_PantryItems_UserId_IngredientId");

            migrationBuilder.AddForeignKey(
                name: "FK_PantryItems_Users_UserId",
                table: "PantryItems",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
