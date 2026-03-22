using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhereWeFishin.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPolygonAndZoomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Coordinates",
                table: "Pontoons",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DefaultCenterLat",
                table: "FishingSpots",
                type: "float(9)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DefaultCenterLng",
                table: "FishingSpots",
                type: "float(9)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultZoom",
                table: "FishingSpots",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Coordinates",
                table: "Pontoons");

            migrationBuilder.DropColumn(
                name: "DefaultCenterLat",
                table: "FishingSpots");

            migrationBuilder.DropColumn(
                name: "DefaultCenterLng",
                table: "FishingSpots");

            migrationBuilder.DropColumn(
                name: "DefaultZoom",
                table: "FishingSpots");
        }
    }
}
