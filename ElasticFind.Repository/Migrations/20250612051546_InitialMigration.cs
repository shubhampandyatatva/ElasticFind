using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ElasticFind.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.CreateTable(
            //     name: "files",
            //     columns: table => new
            //     {
            //         id = table.Column<int>(type: "integer", nullable: false)
            //             .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            //         filename = table.Column<string>(name: "file_name", type: "character varying(255)", maxLength: 255, nullable: false),
            //         uploaddate = table.Column<DateTime>(name: "upload_date", type: "timestamp without time zone", nullable: true),
            //         filetype = table.Column<string>(name: "file_type", type: "character varying(20)", maxLength: 20, nullable: true)
            //     },
            //     constraints: table =>
            //     {
            //         table.PrimaryKey("files_pkey", x => x.id);
            //     });

            // migrationBuilder.CreateTable(
            //     name: "roles",
            //     columns: table => new
            //     {
            //         id = table.Column<int>(type: "integer", nullable: false)
            //             .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            //         rolename = table.Column<string>(name: "role_name", type: "character varying(20)", maxLength: 20, nullable: false)
            //     },
            //     constraints: table =>
            //     {
            //         table.PrimaryKey("roles_pkey", x => x.id);
            //     });

            // migrationBuilder.CreateTable(
            //     name: "users",
            //     columns: table => new
            //     {
            //         id = table.Column<int>(type: "integer", nullable: false)
            //             .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            //         firstname = table.Column<string>(name: "first_name", type: "character varying(255)", maxLength: 255, nullable: false),
            //         lastname = table.Column<string>(name: "last_name", type: "character varying(255)", maxLength: 255, nullable: true),
            //         username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
            //         email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            //         phone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
            //         profileimage = table.Column<string>(name: "profile_image", type: "text", nullable: true),
            //         roleid = table.Column<int>(name: "role_id", type: "integer", nullable: true),
            //         password = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
            //         isdeleted = table.Column<bool>(type: "boolean", nullable: true),
            //         isactive = table.Column<bool>(type: "boolean", nullable: true)
            //     },
            //     constraints: table =>
            //     {
            //         table.PrimaryKey("users_pkey", x => x.id);
            //         table.ForeignKey(
            //             name: "users_role_id_fkey",
            //             column: x => x.roleid,
            //             principalTable: "roles",
            //             principalColumn: "id");
            //     });

            // migrationBuilder.CreateIndex(
            //     name: "IX_users_role_id",
            //     table: "users",
            //     column: "role_id");

            // migrationBuilder.CreateIndex(
            //     name: "users_email_key",
            //     table: "users",
            //     column: "email",
            //     unique: true);

            // migrationBuilder.CreateIndex(
            //     name: "users_phone_key",
            //     table: "users",
            //     column: "phone",
            //     unique: true);

            // migrationBuilder.CreateIndex(
            //     name: "users_username_key",
            //     table: "users",
            //     column: "username",
            //     unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropTable(
            //     name: "files");

            // migrationBuilder.DropTable(
            //     name: "users");

            // migrationBuilder.DropTable(
            //     name: "roles");
        }
    }
}
