using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InternalAudits_AuditPlanItems_AuditPlanItemId",
                table: "InternalAudits");

            migrationBuilder.DropForeignKey(
                name: "FK_InternalAudits_Departments_DepartmentId",
                table: "InternalAudits");

            migrationBuilder.DropIndex(
                name: "IX_InternalAudits_AuditPlanItemId",
                table: "InternalAudits");

            migrationBuilder.DropIndex(
                name: "IX_InternalAudits_DepartmentId",
                table: "InternalAudits");

            migrationBuilder.DropColumn(
                name: "AuditPlanItemId",
                table: "InternalAudits");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "InternalAudits");

            migrationBuilder.AddColumn<string>(
                name: "AuditSource",
                table: "AuditFindings",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ExternalAuditId",
                table: "AuditFindings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureNote",
                table: "AuditFindingActions",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DelayReason",
                table: "AuditFindingActions",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConfigChangeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChangedBy = table.Column<string>(type: "TEXT", nullable: false),
                    SettingKey = table.Column<string>(type: "TEXT", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigChangeLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalAuditBodies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalAuditBodies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AuditingBody = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Standard = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    AuditDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ResponsibleDeptId = table.Column<int>(type: "INTEGER", nullable: true),
                    ResponsibleUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Score = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReportFilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedById = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalAudits_Departments_ResponsibleDeptId",
                        column: x => x.ResponsibleDeptId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExternalAudits_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalAudits_Users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FindingActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FindingId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FindingActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FindingActivityLogs_AuditFindings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "AuditFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FindingActivityLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RiskFindingLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RiskId = table.Column<int>(type: "INTEGER", nullable: false),
                    FindingId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedById = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskFindingLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskFindingLinks_AuditFindings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "AuditFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskFindingLinks_Risks_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskFindingLinks_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserExternalAuditBodies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    AuditingBody = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CanEdit = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExternalAuditBodies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserExternalAuditBodies_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAuditBodies_IsActive",
                table: "ExternalAuditBodies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAuditBodies_Name",
                table: "ExternalAuditBodies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAudits_AuditDate",
                table: "ExternalAudits",
                column: "AuditDate");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAudits_AuditingBody",
                table: "ExternalAudits",
                column: "AuditingBody");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAudits_CreatedById",
                table: "ExternalAudits",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAudits_ResponsibleDeptId",
                table: "ExternalAudits",
                column: "ResponsibleDeptId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAudits_ResponsibleUserId",
                table: "ExternalAudits",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalAudits_Status",
                table: "ExternalAudits",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FindingActivityLogs_FindingId",
                table: "FindingActivityLogs",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_FindingActivityLogs_UserId",
                table: "FindingActivityLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskFindingLinks_CreatedById",
                table: "RiskFindingLinks",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_RiskFindingLinks_FindingId",
                table: "RiskFindingLinks",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskFindingLinks_RiskId_FindingId",
                table: "RiskFindingLinks",
                columns: new[] { "RiskId", "FindingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserExternalAuditBodies_UserId_AuditingBody",
                table: "UserExternalAuditBodies",
                columns: new[] { "UserId", "AuditingBody" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigChangeLogs");

            migrationBuilder.DropTable(
                name: "ExternalAuditBodies");

            migrationBuilder.DropTable(
                name: "ExternalAudits");

            migrationBuilder.DropTable(
                name: "FindingActivityLogs");

            migrationBuilder.DropTable(
                name: "RiskFindingLinks");

            migrationBuilder.DropTable(
                name: "UserExternalAuditBodies");

            migrationBuilder.DropColumn(
                name: "AuditSource",
                table: "AuditFindings");

            migrationBuilder.DropColumn(
                name: "ExternalAuditId",
                table: "AuditFindings");

            migrationBuilder.DropColumn(
                name: "ClosureNote",
                table: "AuditFindingActions");

            migrationBuilder.DropColumn(
                name: "DelayReason",
                table: "AuditFindingActions");

            migrationBuilder.AddColumn<int>(
                name: "AuditPlanItemId",
                table: "InternalAudits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "InternalAudits",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalAudits_AuditPlanItemId",
                table: "InternalAudits",
                column: "AuditPlanItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalAudits_DepartmentId",
                table: "InternalAudits",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_InternalAudits_AuditPlanItems_AuditPlanItemId",
                table: "InternalAudits",
                column: "AuditPlanItemId",
                principalTable: "AuditPlanItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_InternalAudits_Departments_DepartmentId",
                table: "InternalAudits",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
