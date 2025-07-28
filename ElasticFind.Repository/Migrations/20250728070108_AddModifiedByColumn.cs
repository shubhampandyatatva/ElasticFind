using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElasticFind.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddModifiedByColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "categories");

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "categories",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "categories");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
