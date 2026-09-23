using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResticManager.PgSql.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryStatsPersist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatsJson",
                table: "Repositories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatsUpdatedAt",
                table: "Repositories",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatsJson",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "StatsUpdatedAt",
                table: "Repositories");
        }
    }
}
