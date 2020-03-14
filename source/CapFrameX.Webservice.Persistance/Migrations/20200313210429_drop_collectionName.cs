using Microsoft.EntityFrameworkCore.Migrations;

namespace CapFrameX.Webservice.Persistance.Migrations
{
    public partial class drop_collectionName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "SessionCollections");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "SessionCollections",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
