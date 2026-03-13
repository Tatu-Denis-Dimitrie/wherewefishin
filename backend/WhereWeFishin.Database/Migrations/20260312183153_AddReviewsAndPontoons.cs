using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhereWeFishin.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewsAndPontoons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pontoons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FishingSpotId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SouthWestLat = table.Column<double>(type: "float(18)", precision: 18, scale: 15, nullable: false),
                    SouthWestLng = table.Column<double>(type: "float(18)", precision: 18, scale: 15, nullable: false),
                    NorthEastLat = table.Column<double>(type: "float(18)", precision: 18, scale: 15, nullable: false),
                    NorthEastLng = table.Column<double>(type: "float(18)", precision: 18, scale: 15, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pontoons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pontoons_FishingSpots_FishingSpotId",
                        column: x => x.FishingSpotId,
                        principalTable: "FishingSpots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FishingSpotId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_FishingSpots_FishingSpotId",
                        column: x => x.FishingSpotId,
                        principalTable: "FishingSpots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pontoons_FishingSpotId",
                table: "Pontoons",
                column: "FishingSpotId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_FishingSpotId",
                table: "Reviews",
                column: "FishingSpotId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId_FishingSpotId",
                table: "Reviews",
                columns: new[] { "UserId", "FishingSpotId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pontoons");

            migrationBuilder.DropTable(
                name: "Reviews");
        }
    }
}
