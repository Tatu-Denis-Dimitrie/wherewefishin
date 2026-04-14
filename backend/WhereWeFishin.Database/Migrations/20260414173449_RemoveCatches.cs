using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhereWeFishin.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Catches");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Catches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FishingSpotId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CaughtAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FishSpecies = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Length = table.Column<double>(type: "float(8)", precision: 8, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Weight = table.Column<double>(type: "float(8)", precision: 8, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Catches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Catches_FishingSpots_FishingSpotId",
                        column: x => x.FishingSpotId,
                        principalTable: "FishingSpots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Catches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Catches_FishingSpotId",
                table: "Catches",
                column: "FishingSpotId");

            migrationBuilder.CreateIndex(
                name: "IX_Catches_UserId",
                table: "Catches",
                column: "UserId");
        }
    }
}
