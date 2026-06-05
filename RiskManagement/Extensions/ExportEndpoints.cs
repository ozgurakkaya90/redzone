using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Services;

namespace RiskManagement.Extensions;

public static class ExportEndpoints
{
    public static WebApplication MapExportEndpoints(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        // ── Risk Envanteri ────────────────────────────────────────────────────
        app.MapGet("/export/risks/excel", (AuthService authSvc, RiskService riskSvc, ExportService exportSvc, ClaimsPrincipal user) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();
            var risks = riskSvc.GetForUser(userObj);
            var bytes = exportSvc.ExportRisksToExcel(risks);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Risk_Kaydi_{DateTime.Now:yyyyMMdd}.xlsx");
        }).RequireAuthorization();

        app.MapGet("/export/risks/pdf", (AuthService authSvc, RiskService riskSvc, ExportService exportSvc, ClaimsPrincipal user) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();
            var risks = riskSvc.GetForUser(userObj);
            var bytes = exportSvc.ExportRisksToPdf(risks);
            return Results.File(bytes, "application/pdf",
                $"Risk_Kaydi_{DateTime.Now:yyyyMMdd}.pdf");
        }).RequireAuthorization();

        // ── Denetim Bulguları ─────────────────────────────────────────────────
        app.MapGet("/export/findings/excel", (AuthService authSvc, AuditService auditSvc, ExportService exportSvc, ClaimsPrincipal user) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();
            var findings = auditSvc.GetFindingsForUser(userObj);
            var bytes = exportSvc.ExportFindingsToExcel(findings);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Denetim_Bulgulari_{DateTime.Now:yyyyMMdd}.xlsx");
        }).RequireAuthorization();

        app.MapGet("/export/findings/pdf", (AuthService authSvc, AuditService auditSvc, ExportService exportSvc, ClaimsPrincipal user) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();
            var findings = auditSvc.GetFindingsForUser(userObj);
            var bytes = exportSvc.ExportFindingsToPdf(findings);
            return Results.File(bytes, "application/pdf",
                $"Denetim_Bulgulari_{DateTime.Now:yyyyMMdd}.pdf");
        }).RequireAuthorization();

        // ── Kontrol Planı ─────────────────────────────────────────────────────
        app.MapGet("/export/controls/excel", (AuthService authSvc, RiskService riskSvc, AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();
            var accessibleRiskIds = riskSvc.GetForUser(userObj).Select(r => r.Id).ToHashSet();
            var controls = db.Controls
                .Include(c => c.Risk).Include(c => c.EnteredBy).Include(c => c.OwnerDept)
                .Where(c => accessibleRiskIds.Contains(c.RiskId)).ToList();
            var bytes = exportSvc.ExportControlsToExcel(controls);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Kontrol_Plani_{DateTime.Now:yyyyMMdd}.xlsx");
        }).RequireAuthorization();

        app.MapGet("/export/controls/pdf", (AuthService authSvc, RiskService riskSvc, AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();
            var accessibleRiskIds = riskSvc.GetForUser(userObj).Select(r => r.Id).ToHashSet();
            var controls = db.Controls
                .Include(c => c.Risk).Include(c => c.EnteredBy).Include(c => c.OwnerDept)
                .Where(c => accessibleRiskIds.Contains(c.RiskId)).ToList();
            var bytes = exportSvc.ExportControlsToPdf(controls);
            return Results.File(bytes, "application/pdf",
                $"Kontrol_Plani_{DateTime.Now:yyyyMMdd}.pdf");
        }).RequireAuthorization();

        // ── Risk Aksiyon Planları ─────────────────────────────────────────────
        app.MapGet("/export/action-plans/excel", (AuthService authSvc, RiskService riskSvc, AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();
            var accessibleRiskIds = riskSvc.GetForUser(userObj).Select(r => r.Id).ToHashSet();
            var plans = db.ActionPlans
                .Include(a => a.Risk).Include(a => a.CreatedBy).Include(a => a.OwnerDept)
                .Where(a => accessibleRiskIds.Contains(a.RiskId)).ToList();
            var bytes = exportSvc.ExportActionPlansToExcel(plans);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Aksiyon_Planlari_{DateTime.Now:yyyyMMdd}.xlsx");
        }).RequireAuthorization();

        app.MapGet("/export/action-plans/pdf", (AuthService authSvc, RiskService riskSvc, AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();
            var accessibleRiskIds = riskSvc.GetForUser(userObj).Select(r => r.Id).ToHashSet();
            var plans = db.ActionPlans
                .Include(a => a.Risk).Include(a => a.CreatedBy).Include(a => a.OwnerDept)
                .Where(a => accessibleRiskIds.Contains(a.RiskId)).ToList();
            var bytes = exportSvc.ExportActionPlansToPdf(plans);
            return Results.File(bytes, "application/pdf",
                $"Aksiyon_Planlari_{DateTime.Now:yyyyMMdd}.pdf");
        }).RequireAuthorization();

        // ── Denetim Aksiyon Planları ──────────────────────────────────────────
        app.MapGet("/export/audit-actions/excel", (AuthService authSvc, AuditService auditSvc, AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();
            var accessibleFindingIds = auditSvc.GetFindingsForUser(userObj).Select(f => f.Id).ToHashSet();
            var actions = db.AuditFindingActions
                .Include(a => a.Finding).Include(a => a.CreatedBy)
                .Where(a => accessibleFindingIds.Contains(a.FindingId)).ToList();
            var bytes = exportSvc.ExportAuditActionsToExcel(actions);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Denetim_Aksiyonlari_{DateTime.Now:yyyyMMdd}.xlsx");
        }).RequireAuthorization();

        app.MapGet("/export/audit-actions/pdf", (AuthService authSvc, AuditService auditSvc, AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();
            var accessibleFindingIds = auditSvc.GetFindingsForUser(userObj).Select(f => f.Id).ToHashSet();
            var actions = db.AuditFindingActions
                .Include(a => a.Finding).Include(a => a.CreatedBy)
                .Where(a => accessibleFindingIds.Contains(a.FindingId)).ToList();
            var bytes = exportSvc.ExportAuditActionsToPdf(actions);
            return Results.File(bytes, "application/pdf",
                $"Denetim_Aksiyonlari_{DateTime.Now:yyyyMMdd}.pdf");
        }).RequireAuthorization();

        // ── Etik Bildirimler ──────────────────────────────────────────────────
        app.MapGet("/export/ethics/excel", (AuthService authSvc, AppDbContext db, ExportService exportSvc, ClaimsPrincipal user, HttpContext ctx) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();
            // Etik bildirimler hassastır; UI'da görüntüleme audit_manager/ethics_board/admin ile sınırlı.
            // Dışa aktarım da aynı yetkiyi (denetim veya kurul değerlendirme izni) gerektirmeli —
            // yalnızca "ethics.read" olan denetçinin tüm ihbarları toplu indirmesini engeller.
            if (!authSvc.HasPermission(userObj, "ethics.audit_review")
                && !authSvc.HasPermission(userObj, "ethics.board_review"))
            {
                logger.LogWarning("Etik export erişim reddi — kullanıcı: {User}, IP: {Ip}",
                    userObj.Username, ctx.Connection.RemoteIpAddress);
                return Results.Forbid();
            }
            var reports = db.EthicsReports.ToList();
            var bytes = exportSvc.ExportEthicsToExcel(reports);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Etik_Bildirimler_{DateTime.Now:yyyyMMdd}.xlsx");
        }).RequireAuthorization();

        app.MapGet("/export/ethics/pdf", (AuthService authSvc, AppDbContext db, ExportService exportSvc, ClaimsPrincipal user, HttpContext ctx) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();
            // Etik bildirimler hassastır; UI görüntüleme yetkisiyle (denetim/kurul değerlendirme) hizalı.
            if (!authSvc.HasPermission(userObj, "ethics.audit_review")
                && !authSvc.HasPermission(userObj, "ethics.board_review"))
            {
                logger.LogWarning("Etik export erişim reddi — kullanıcı: {User}, IP: {Ip}",
                    userObj.Username, ctx.Connection.RemoteIpAddress);
                return Results.Forbid();
            }
            var reports = db.EthicsReports.ToList();
            var bytes = exportSvc.ExportEthicsToPdf(reports);
            return Results.File(bytes, "application/pdf",
                $"Etik_Bildirimler_{DateTime.Now:yyyyMMdd}.pdf");
        }).RequireAuthorization();

        // ── Dış Denetim Aksiyon Planları ─────────────────────────────────────
        app.MapGet("/export/external-audit-actions/excel",
            (AuthService authSvc, ExternalAuditService extSvc, AppDbContext db,
             ExportService exportSvc, ClaimsPrincipal user) =>
            {
                var userObj = authSvc.ActiveUserFromPrincipal(user);
                if (userObj is null) return Results.Unauthorized();

                var allowed = extSvc.GetAllowedBodies(userObj);
                var auditQ = db.ExternalAudits.AsQueryable();
                if (allowed != null) auditQ = auditQ.Where(a => allowed.Contains(a.AuditingBody));
                var auditIds = auditQ.Select(a => a.Id).ToList();

                var findingIds = db.AuditFindings
                    .Where(f => f.ExternalAuditId.HasValue && auditIds.Contains(f.ExternalAuditId.Value))
                    .Select(f => f.Id).ToList();

                var actions = db.AuditFindingActions
                    .Include(a => a.Finding).ThenInclude(f => f!.ExternalAudit)
                    .Where(a => findingIds.Contains(a.FindingId))
                    .OrderBy(a => a.Finding.ExternalAudit!.AuditingBody)
                    .ThenBy(a => a.DueDate)
                    .ToList();

                var bytes = exportSvc.ExportExternalAuditActionsToExcel(actions);
                return Results.File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"DisD_AksiyonPlanlari_{DateTime.Now:yyyyMMdd}.xlsx");
            }).RequireAuthorization();

        // ── Dış Denetim Uygunsuzluk Aksiyon Planı ────────────────────────────
        app.MapGet("/export/external-nonconformities/excel",
            (AuthService authSvc, ExternalAuditService extSvc, AppDbContext db,
             ExportService exportSvc, ClaimsPrincipal user) =>
            {
                var userObj = authSvc.ActiveUserFromPrincipal(user);
                if (userObj is null) return Results.Unauthorized();

                var allowed = extSvc.GetAllowedBodies(userObj);
                var query = db.AuditFindings
                    .Include(f => f.ExternalAudit)
                    .Include(f => f.Actions)
                    .Where(f => f.AuditSource == "external" && f.ExternalAuditId != null);

                if (allowed != null)
                    query = query.Where(f => allowed.Contains(f.ExternalAudit!.AuditingBody));

                var findings = query
                    .OrderBy(f => f.ExternalAudit!.AuditDate)
                    .ThenBy(f => f.Id)
                    .ToList();

                var bytes = exportSvc.ExportExternalAuditNonconformitiesToExcel(findings);
                return Results.File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"ResmiDenetim_Uygunsuzluk_{DateTime.Now:yyyyMMdd}.xlsx");
            }).RequireAuthorization();

        // ── Risk Kütüphanesi ──────────────────────────────────────────────────
        // Kütüphane şablonları hassas değildir; kimlik doğrulaması yeterlidir.
        // Değerlendirme değişirse risk.read izni eklenmelidir.
        app.MapGet("/export/library/excel", (AuthService authSvc, RiskLibraryService libSvc, ClaimsPrincipal user) =>
        {
            if (authSvc.ActiveUserFromPrincipal(user) is null) return Results.Unauthorized();
            var items = libSvc.Search(null, null, null, null);
            var bytes = libSvc.ExportToExcel(items);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Risk_Kutuphanesi_{DateTime.Now:yyyyMMdd}.xlsx");
        }).RequireAuthorization();

        return app;
    }
}
