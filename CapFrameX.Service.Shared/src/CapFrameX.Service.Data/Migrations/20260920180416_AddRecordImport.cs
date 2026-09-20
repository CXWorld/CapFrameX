using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapFrameX.Service.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImportedFrom",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "RunIndex",
                table: "SessionRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Hash",
                table: "Sessions",
                column: "Hash",
                unique: true,
                filter: "\"Hash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRuns_SessionId_RunIndex",
                table: "SessionRuns",
                columns: new[] { "SessionId", "RunIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_Hash",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_SessionRuns_SessionId_RunIndex",
                table: "SessionRuns");

            migrationBuilder.DropColumn(
                name: "ImportedFrom",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RunIndex",
                table: "SessionRuns");
        }
    }
}
