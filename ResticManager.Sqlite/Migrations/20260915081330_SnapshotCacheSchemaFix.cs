using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResticManager.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotCacheSchemaFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BackupSnapshots_BackupRecords_BackupRecordId",
                table: "BackupSnapshots");

            migrationBuilder.AddColumn<string>(
                name: "SummaryJson",
                table: "BackupSnapshots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BackupSnapshots_BackupRecords_BackupRecordId",
                table: "BackupSnapshots",
                column: "BackupRecordId",
                principalTable: "BackupRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BackupSnapshots_BackupRecords_BackupRecordId",
                table: "BackupSnapshots");

            migrationBuilder.DropColumn(
                name: "SummaryJson",
                table: "BackupSnapshots");

            migrationBuilder.AddForeignKey(
                name: "FK_BackupSnapshots_BackupRecords_BackupRecordId",
                table: "BackupSnapshots",
                column: "BackupRecordId",
                principalTable: "BackupRecords",
                principalColumn: "Id");
        }
    }
}
