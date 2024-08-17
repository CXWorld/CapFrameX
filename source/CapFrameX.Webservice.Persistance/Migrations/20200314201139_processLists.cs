using Microsoft.EntityFrameworkCore.Migrations;

namespace CapFrameX.Webservice.Persistance.Migrations
{
    public partial class processLists : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameList",
                columns: table => new
                {
                    Process = table.Column<string>(nullable: false),
                    DisplayName = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameList", x => x.Process);
                });

            migrationBuilder.CreateTable(
                name: "IgnoreList",
                columns: table => new
                {
                    Process = table.Column<string>(nullable: false),
                    DisplayName = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IgnoreList", x => x.Process);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameList");

            migrationBuilder.DropTable(
                name: "IgnoreList");
        }
    }
}
