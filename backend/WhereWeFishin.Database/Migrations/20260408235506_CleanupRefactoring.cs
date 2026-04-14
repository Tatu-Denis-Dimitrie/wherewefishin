using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhereWeFishin.Database.Migrations
{
    /// <inheritdoc />
    public partial class CleanupRefactoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SpotEmployees_UserId_FishingSpotId",
                table: "SpotEmployees");

            migrationBuilder.CreateIndex(
                name: "IX_SpotEmployees_UserId_FishingSpotId",
                table: "SpotEmployees",
                columns: new[] { "UserId", "FishingSpotId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SpotEmployees_UserId_FishingSpotId",
                table: "SpotEmployees");

            migrationBuilder.CreateIndex(
                name: "IX_SpotEmployees_UserId_FishingSpotId",
                table: "SpotEmployees",
                columns: new[] { "UserId", "FishingSpotId" },
                unique: true);
        }
    }
}
