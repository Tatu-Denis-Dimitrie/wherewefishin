using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhereWeFishin.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPontoonToFishingSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PontoonId",
                table: "FishingSessions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FishingSessions_PontoonId",
                table: "FishingSessions",
                column: "PontoonId");

            migrationBuilder.AddForeignKey(
                name: "FK_FishingSessions_Pontoons_PontoonId",
                table: "FishingSessions",
                column: "PontoonId",
                principalTable: "Pontoons",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FishingSessions_Pontoons_PontoonId",
                table: "FishingSessions");

            migrationBuilder.DropIndex(
                name: "IX_FishingSessions_PontoonId",
                table: "FishingSessions");

            migrationBuilder.DropColumn(
                name: "PontoonId",
                table: "FishingSessions");
        }
    }
}
