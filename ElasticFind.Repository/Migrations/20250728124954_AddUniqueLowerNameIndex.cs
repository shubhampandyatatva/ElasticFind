using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElasticFind.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueLowerNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX unique_lower_name ON categories (LOWER('categories.Name'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX unique_lower_name;");
        }
    }
}
