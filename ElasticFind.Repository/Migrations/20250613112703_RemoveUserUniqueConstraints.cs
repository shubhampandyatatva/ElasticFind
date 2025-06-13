using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElasticFind.Repository.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserUniqueConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "users_email_key",
                table: "users");

            migrationBuilder.DropIndex(
                name: "users_phone_key",
                table: "users");

            migrationBuilder.DropIndex(
                name: "users_username_key",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "users_email_key",
                table: "users",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "users_phone_key",
                table: "users",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "users_username_key",
                table: "users",
                column: "username");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "users_email_key",
                table: "users");

            migrationBuilder.DropIndex(
                name: "users_phone_key",
                table: "users");

            migrationBuilder.DropIndex(
                name: "users_username_key",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "users_email_key",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "users_phone_key",
                table: "users",
                column: "phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "users_username_key",
                table: "users",
                column: "username",
                unique: true);
        }
    }
}
