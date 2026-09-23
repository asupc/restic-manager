using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResticManager.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ShortId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    RepositoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    BackupRecordId = table.Column<int>(type: "INTEGER", nullable: true),
                    Time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tree = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Hostname = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Uid = table.Column<int>(type: "INTEGER", nullable: true),
                    Gid = table.Column<int>(type: "INTEGER", nullable: true),
                    ProgramVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ParentSnapshotId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PathsJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TagsJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupSnapshots_BackupRecords_BackupRecordId",
                        column: x => x.BackupRecordId,
                        principalTable: "BackupRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BackupSnapshots_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupSnapshots_BackupRecordId",
                table: "BackupSnapshots",
                column: "BackupRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupSnapshots_RepositoryId_SnapshotId",
                table: "BackupSnapshots",
                columns: new[] { "RepositoryId", "SnapshotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackupSnapshots_RepositoryId_Time",
                table: "BackupSnapshots",
                columns: new[] { "RepositoryId", "Time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupSnapshots");
        }
    }
}
