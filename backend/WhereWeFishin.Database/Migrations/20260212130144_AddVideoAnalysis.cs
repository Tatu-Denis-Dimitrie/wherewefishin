using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhereWeFishin.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    VideoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Duration = table.Column<double>(type: "float", nullable: false),
                    TotalFrames = table.Column<int>(type: "int", nullable: false),
                    Fps = table.Column<int>(type: "int", nullable: false),
                    TotalDetections = table.Column<int>(type: "int", nullable: false),
                    DominantFishType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DominantFishCount = table.Column<int>(type: "int", nullable: false),
                    FishCountsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnalyzedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoAnalyses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalyses_AnalyzedAt",
                table: "VideoAnalyses",
                column: "AnalyzedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalyses_Status",
                table: "VideoAnalyses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalyses_UserId",
                table: "VideoAnalyses",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoAnalyses");
        }
    }
}
