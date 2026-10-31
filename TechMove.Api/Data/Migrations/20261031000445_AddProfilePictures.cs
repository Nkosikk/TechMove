using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TechMove.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfilePictures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ProfilePicture",
                table: "AspNetUsers",
                type: "varbinary(max)",
                maxLength: 1048576,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePictureContentType",
                table: "AspNetUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfilePicture",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfilePictureContentType",
                table: "AspNetUsers");
        }
    }
}
