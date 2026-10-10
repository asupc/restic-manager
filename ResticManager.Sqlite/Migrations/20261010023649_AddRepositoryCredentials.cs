using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResticManager.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CredentialsJson",
                table: "Repositories",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CredentialsJson",
                table: "Repositories");
        }
    }
}
