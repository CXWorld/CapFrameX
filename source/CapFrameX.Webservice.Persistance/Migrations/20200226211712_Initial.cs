using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CapFrameX.Webservice.Persistance.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessionCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    UserId = table.Column<Guid>(nullable: true),
                    Timestamp = table.Column<DateTime>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionProxy",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    SessionCollectionId = table.Column<Guid>(nullable: false),
                    Session = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionProxy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionProxy_SessionCollections_SessionCollectionId",
                        column: x => x.SessionCollectionId,
                        principalTable: "SessionCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionProxy_SessionCollectionId",
                table: "SessionProxy",
                column: "SessionCollectionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionProxy");

            migrationBuilder.DropTable(
                name: "SessionCollections");
        }
    }
}
