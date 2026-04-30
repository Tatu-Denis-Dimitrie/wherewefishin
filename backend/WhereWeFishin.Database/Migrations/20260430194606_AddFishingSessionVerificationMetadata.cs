using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhereWeFishin.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFishingSessionVerificationMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "FishingSessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerifiedByUserId",
                table: "FishingSessions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FishingSessions_VerifiedAt",
                table: "FishingSessions",
                column: "VerifiedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FishingSessions_VerifiedByUserId",
                table: "FishingSessions",
                column: "VerifiedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FishingSessions_VerifiedAt",
                table: "FishingSessions");

            migrationBuilder.DropIndex(
                name: "IX_FishingSessions_VerifiedByUserId",
                table: "FishingSessions");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "FishingSessions");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "FishingSessions");
        }
    }
}
