using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResticManager.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryCheckRecordAndKeepYearly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "KeepYearly",
                table: "BackupRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "KeepYearlyCount",
                table: "BackupRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RepositoryCheckRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    Output = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryCheckRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryCheckRecords_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryCheckRecords_RepositoryId_CheckedAt",
                table: "RepositoryCheckRecords",
                columns: new[] { "RepositoryId", "CheckedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryCheckRecords");

            migrationBuilder.DropColumn(
                name: "KeepYearly",
                table: "BackupRules");

            migrationBuilder.DropColumn(
                name: "KeepYearlyCount",
                table: "BackupRules");
        }
    }
}
