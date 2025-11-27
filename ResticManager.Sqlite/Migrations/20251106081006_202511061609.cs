using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResticManager.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class _202511061609 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupFile");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupFile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BackupTaskId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    SnapshotId = table.Column<string>(type: "TEXT", nullable: true),
                    SnapshotTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupFile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupFile_BackupTasks_BackupTaskId",
                        column: x => x.BackupTaskId,
                        principalTable: "BackupTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupFile_BackupTaskId",
                table: "BackupFile",
                column: "BackupTaskId");
        }
    }
}
