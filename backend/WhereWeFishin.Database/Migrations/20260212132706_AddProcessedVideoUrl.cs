using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhereWeFishin.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessedVideoUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessedVideoUrl",
                table: "VideoAnalyses",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessedVideoUrl",
                table: "VideoAnalyses");
        }
    }
}
