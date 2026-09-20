using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapFrameX.Service.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "Sessions",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrameCount",
                table: "Sessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDisplayChange",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasPcLatency",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IndexVersion",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RunCount",
                table: "Sessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFilePath",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceFileSize",
                table: "Sessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceModifiedUtc",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SparklineJson",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SensorDataJson",
                table: "SessionRuns",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "CaptureDataJson",
                table: "SessionRuns",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_IndexVersion",
                table: "Sessions",
                column: "IndexVersion");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_SourceFilePath",
                table: "Sessions",
                column: "SourceFilePath",
                unique: true,
                filter: "\"SourceFilePath\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_IndexVersion",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_SourceFilePath",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "FrameCount",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "HasDisplayChange",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "HasPcLatency",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IndexVersion",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RunCount",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SourceFilePath",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SourceFileSize",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SourceModifiedUtc",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SparklineJson",
                table: "Sessions");

            migrationBuilder.AlterColumn<string>(
                name: "SensorDataJson",
                table: "SessionRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CaptureDataJson",
                table: "SessionRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
