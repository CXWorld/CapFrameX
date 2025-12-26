using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapFrameX.Service.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Suites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SuiteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GameName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Comment = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Processor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Motherboard = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SystemRam = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Gpu = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    GpuCount = table.Column<int>(type: "INTEGER", nullable: true),
                    GpuCoreClock = table.Column<int>(type: "INTEGER", nullable: true),
                    GpuMemoryClock = table.Column<int>(type: "INTEGER", nullable: true),
                    BaseDriverVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    DriverPackage = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GpuDriverVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Os = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ApiInfo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ResizableBar = table.Column<bool>(type: "INTEGER", nullable: true),
                    WinGameMode = table.Column<bool>(type: "INTEGER", nullable: true),
                    Hags = table.Column<bool>(type: "INTEGER", nullable: true),
                    PresentationMode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ResolutionInfo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Suites_SuiteId",
                        column: x => x.SuiteId,
                        principalTable: "Suites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PresentMonRuntime = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SampleTime = table.Column<double>(type: "REAL", nullable: false),
                    CaptureDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    SensorDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    RtssFrameTimesJson = table.Column<string>(type: "TEXT", nullable: true),
                    PmdGpuPowerJson = table.Column<string>(type: "TEXT", nullable: true),
                    PmdCpuPowerJson = table.Column<string>(type: "TEXT", nullable: true),
                    PmdSystemPowerJson = table.Column<string>(type: "TEXT", nullable: true),
                    MaxFps = table.Column<double>(type: "REAL", nullable: true),
                    P99Fps = table.Column<double>(type: "REAL", nullable: true),
                    P95Fps = table.Column<double>(type: "REAL", nullable: true),
                    AverageFps = table.Column<double>(type: "REAL", nullable: true),
                    MedianFps = table.Column<double>(type: "REAL", nullable: true),
                    P5Fps = table.Column<double>(type: "REAL", nullable: true),
                    P1Fps = table.Column<double>(type: "REAL", nullable: true),
                    P0_1Fps = table.Column<double>(type: "REAL", nullable: true),
                    P0_01Fps = table.Column<double>(type: "REAL", nullable: true),
                    AvgCpuTemp = table.Column<double>(type: "REAL", nullable: true),
                    AvgGpuTemp = table.Column<double>(type: "REAL", nullable: true),
                    AvgCpuPower = table.Column<double>(type: "REAL", nullable: true),
                    AvgGpuPower = table.Column<double>(type: "REAL", nullable: true),
                    AvgCpuUsage = table.Column<double>(type: "REAL", nullable: true),
                    AvgGpuUsage = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionRuns_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionRuns_AverageFps",
                table: "SessionRuns",
                column: "AverageFps");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRuns_CreatedAt",
                table: "SessionRuns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRuns_P1Fps",
                table: "SessionRuns",
                column: "P1Fps");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRuns_P99Fps",
                table: "SessionRuns",
                column: "P99Fps");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRuns_SessionId",
                table: "SessionRuns",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRuns_SessionId_CreatedAt",
                table: "SessionRuns",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CreatedAt",
                table: "Sessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_GameName",
                table: "Sessions",
                column: "GameName");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_GameName_CreatedAt",
                table: "Sessions",
                columns: new[] { "GameName", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Gpu",
                table: "Sessions",
                column: "Gpu");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ProcessName",
                table: "Sessions",
                column: "ProcessName");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Processor",
                table: "Sessions",
                column: "Processor");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_SuiteId_CreatedAt",
                table: "Sessions",
                columns: new[] { "SuiteId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Suites_CreatedAt",
                table: "Suites",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Suites_Type",
                table: "Suites",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Suites_Type_CreatedAt",
                table: "Suites",
                columns: new[] { "Type", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionRuns");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Suites");
        }
    }
}
