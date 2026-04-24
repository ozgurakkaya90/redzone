using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppConfigs",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConfigs", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "LdapConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Server = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Port = table.Column<int>(type: "int", nullable: false),
                    UseSsl = table.Column<bool>(type: "bit", nullable: false),
                    UseTls = table.Column<bool>(type: "bit", nullable: false),
                    Timeout = table.Column<int>(type: "int", nullable: false),
                    BindDn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BindPassword = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserSearchBase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserSearchFilter = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttrDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttrEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttrDepartment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AutoCreateUsers = table.Column<bool>(type: "bit", nullable: false),
                    DefaultRole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LdapConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Role = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EthicsReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReportCategory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AuditDecision = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditReviewedById = table.Column<int>(type: "int", nullable: true),
                    AuditReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EthicsDecision = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EthicsNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EthicsReviewedById = table.Column<int>(type: "int", nullable: true),
                    EthicsReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EthicsReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EthicsReports_Users_AuditReviewedById",
                        column: x => x.AuditReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EthicsReports_Users_EthicsReviewedById",
                        column: x => x.EthicsReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InternalAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Period = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuditType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeadAuditorId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalAudits_Users_LeadAuditorId",
                        column: x => x.LeadAuditorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Used = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Risks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsibleUnit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RiskStrategy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProposedById = table.Column<int>(type: "int", nullable: true),
                    ProposerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProposedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Risks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Risks_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Risks_Users_ProposedById",
                        column: x => x.ProposedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EthicsAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    OriginalFilename = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoredFilename = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EthicsAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EthicsAttachments_EthicsReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "EthicsReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditFindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditPeriod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AuditorId = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: true),
                    InternalAuditId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditFindings_InternalAudits_InternalAuditId",
                        column: x => x.InternalAuditId,
                        principalTable: "InternalAudits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AuditFindings_Users_AuditorId",
                        column: x => x.AuditorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditFindings_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ActionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiskId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Responsible = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionPlans_Risks_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionPlans_Users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Controls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiskId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ControlType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Effectiveness = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Frequency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnteredById = table.Column<int>(type: "int", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Controls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Controls_Risks_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Controls_Users_EnteredById",
                        column: x => x.EnteredById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Evaluations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiskId = table.Column<int>(type: "int", nullable: false),
                    EvalType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Probability = table.Column<double>(type: "float", nullable: false),
                    Exposure = table.Column<double>(type: "float", nullable: false),
                    Consequence = table.Column<double>(type: "float", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvaluatedById = table.Column<int>(type: "int", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Evaluations_Risks_RiskId",
                        column: x => x.RiskId,
                        principalTable: "Risks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Evaluations_Users_EvaluatedById",
                        column: x => x.EvaluatedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClosureRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FindingId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedById = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewedById = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClosureRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClosureRequests_AuditFindings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "AuditFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClosureRequests_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClosureRequests_Users_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionPlans_CreatedById",
                table: "ActionPlans",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ActionPlans_RiskId",
                table: "ActionPlans",
                column: "RiskId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_AuditorId",
                table: "AuditFindings",
                column: "AuditorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_InternalAuditId",
                table: "AuditFindings",
                column: "InternalAuditId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditFindings_OwnerId",
                table: "AuditFindings",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosureRequests_FindingId",
                table: "ClosureRequests",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_ClosureRequests_RequestedById",
                table: "ClosureRequests",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_ClosureRequests_ReviewedById",
                table: "ClosureRequests",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Controls_EnteredById",
                table: "Controls",
                column: "EnteredById");

            migrationBuilder.CreateIndex(
                name: "IX_Controls_RiskId",
                table: "Controls",
                column: "RiskId");

            migrationBuilder.CreateIndex(
                name: "IX_EthicsAttachments_ReportId",
                table: "EthicsAttachments",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_EthicsReports_AuditReviewedById",
                table: "EthicsReports",
                column: "AuditReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_EthicsReports_EthicsReviewedById",
                table: "EthicsReports",
                column: "EthicsReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_EvaluatedById",
                table: "Evaluations",
                column: "EvaluatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_RiskId",
                table: "Evaluations",
                column: "RiskId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalAudits_LeadAuditorId",
                table: "InternalAudits",
                column: "LeadAuditorId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId",
                table: "PasswordResetTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_OwnerId",
                table: "Risks",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Risks_ProposedById",
                table: "Risks",
                column: "ProposedById");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_Role_Permission",
                table: "RolePermissions",
                columns: new[] { "Role", "Permission" },
                unique: true);

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
                name: "ActionPlans");

            migrationBuilder.DropTable(
                name: "AppConfigs");

            migrationBuilder.DropTable(
                name: "ClosureRequests");

            migrationBuilder.DropTable(
                name: "Controls");

            migrationBuilder.DropTable(
                name: "EthicsAttachments");

            migrationBuilder.DropTable(
                name: "Evaluations");

            migrationBuilder.DropTable(
                name: "LdapConfigurations");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "AuditFindings");

            migrationBuilder.DropTable(
                name: "EthicsReports");

            migrationBuilder.DropTable(
                name: "Risks");

            migrationBuilder.DropTable(
                name: "InternalAudits");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
