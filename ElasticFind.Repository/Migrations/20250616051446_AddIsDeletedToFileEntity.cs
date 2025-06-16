using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElasticFind.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedToFileEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "files",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "files");
        }
    }
}
