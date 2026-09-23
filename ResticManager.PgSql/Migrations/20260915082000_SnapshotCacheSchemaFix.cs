using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResticManager.PgSql.Migrations
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
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TagsJson",
                table: "BackupSnapshots",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PathsJson",
                table: "BackupSnapshots",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

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

            migrationBuilder.AlterColumn<string>(
                name: "PathsJson",
                table: "BackupSnapshots",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TagsJson",
                table: "BackupSnapshots",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

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
