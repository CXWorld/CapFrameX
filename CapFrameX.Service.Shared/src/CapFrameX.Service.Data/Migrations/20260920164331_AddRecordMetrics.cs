using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapFrameX.Service.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageFps",
                table: "Sessions",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "P1Fps",
                table: "Sessions",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "P99Fps",
                table: "Sessions",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageFps",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "P1Fps",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "P99Fps",
                table: "Sessions");
        }
    }
}
