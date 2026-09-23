using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ResticManager.PgSql.Migrations
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SnapshotId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ShortId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RepositoryId = table.Column<int>(type: "integer", nullable: false),
                    BackupRecordId = table.Column<int>(type: "integer", nullable: true),
                    Time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Tree = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Hostname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Uid = table.Column<int>(type: "integer", nullable: true),
                    Gid = table.Column<int>(type: "integer", nullable: true),
                    ProgramVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ParentSnapshotId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PathsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TagsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
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
