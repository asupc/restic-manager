using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResticManager.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class _202511050842 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IncludePatterns = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ExcludePatterns = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    KeepDaily = table.Column<bool>(type: "INTEGER", nullable: false),
                    KeepDailyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    KeepWeekly = table.Column<bool>(type: "INTEGER", nullable: false),
                    KeepWeeklyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    KeepMonthly = table.Column<bool>(type: "INTEGER", nullable: false),
                    KeepMonthlyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Repositories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsInitialized = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    SourcePath = table.Column<string>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RepositoryType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LastRunStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BackupRuleId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupTasks_BackupRules_BackupRuleId",
                        column: x => x.BackupRuleId,
                        principalTable: "BackupRules",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BackupTasks_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BackupFile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BackupTaskId = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotId = table.Column<string>(type: "TEXT", nullable: true),
                    SnapshotTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "BackupRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BackupTaskId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FilesProcessed = table.Column<long>(type: "INTEGER", nullable: false),
                    DataTransferred = table.Column<long>(type: "INTEGER", nullable: false),
                    Output = table.Column<string>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SummaryJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupRecords_BackupTasks_BackupTaskId",
                        column: x => x.BackupTaskId,
                        principalTable: "BackupTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupFile_BackupTaskId",
                table: "BackupFile",
                column: "BackupTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_BackupTaskId",
                table: "BackupRecords",
                column: "BackupTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupTasks_BackupRuleId",
                table: "BackupTasks",
                column: "BackupRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupTasks_RepositoryId",
                table: "BackupTasks",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupFile");

            migrationBuilder.DropTable(
                name: "BackupRecords");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "BackupTasks");

            migrationBuilder.DropTable(
                name: "BackupRules");

            migrationBuilder.DropTable(
                name: "Repositories");
        }
    }
}
