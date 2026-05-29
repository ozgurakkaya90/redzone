using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Services;

namespace RiskManagement.Extensions;

public static class ImportEndpoints
{
    public static WebApplication MapImportEndpoints(this WebApplication app)
    {
        // ── Şablon İndirme ────────────────────────────────────────────────────
        app.MapGet("/import/template/{module}", (string module, AuthService authSvc, ExportService exportSvc, RiskLibraryService libSvc, ClaimsPrincipal user) =>
        {
            if (authSvc.ActiveUserFromPrincipal(user) is null) return Results.Unauthorized();
            var (bytes, fileName) = module switch
            {
                "risks"         => (exportSvc.GetRiskImportTemplate(),        "Risk_Sablon.xlsx"),
                "controls"      => (exportSvc.GetControlImportTemplate(),     "Kontrol_Sablon.xlsx"),
                "action-plans"  => (exportSvc.GetActionPlanImportTemplate(),  "AksiyonPlan_Sablon.xlsx"),
                "findings"      => (exportSvc.GetFindingImportTemplate(),     "Bulgu_Sablon.xlsx"),
                "audit-actions" => (exportSvc.GetAuditActionImportTemplate(), "DenetimAksiyon_Sablon.xlsx"),
                "ethics"        => (exportSvc.GetEthicsImportTemplate(),      "Etik_Sablon.xlsx"),
                "library"       => (libSvc.GetImportTemplate(),               "Kutuphane_Sablon.xlsx"),
                _               => ((byte[]?)null, "")
            };
            if (bytes is null) return Results.NotFound();
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }).RequireAuthorization();

        // ── İçe Aktarma (API endpoint — UI Blazor DI kullanır) ───────────────
        app.MapPost("/import/{module}", async (string module, HttpRequest request, ImportService importSvc,
            RiskLibraryService libraryImportSvc, AuthService authSvc, RiskService riskSvc, AuditService auditSvc, ClaimsPrincipal user) =>
        {
            var userObj = authSvc.ActiveUserFromPrincipal(user);
            if (userObj is null) return Results.Unauthorized();

            // Modül bazlı yetki kontrolü — sadece login olmak yeterli değil
            var requiredPermission = module switch
            {
                "risks"         => "risk.write",
                "controls"      => "control.write",
                "action-plans"  => "action.write",
                "findings"      => "audit.write",
                "audit-actions" => "audit.write",
                "ethics"        => "ethics.write",
                "library"       => "risk.manage",
                _               => null
            };

            if (requiredPermission is not null && !authSvc.HasPermission(userObj, requiredPermission))
                return Results.Forbid();

            if (!request.HasFormContentType || request.Form.Files.Count == 0)
                return Results.BadRequest(new { error = "Dosya yüklenmedi." });

            var file = request.Form.Files[0];
            if (file.Length == 0)
                return Results.BadRequest(new { error = "Boş dosya." });

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            // controls ve action-plans: risk.manage izni olmayanlar yalnızca kendi erişebildiği risklere yazabilir
            HashSet<int>? allowedRiskIds = null;
            if (module is "controls" or "action-plans")
            {
                if (!authSvc.HasPermission(userObj, "risk.manage"))
                    allowedRiskIds = riskSvc.GetForUser(userObj).Select(r => r.Id).ToHashSet();
            }

            // audit-actions: audit.admin izni olmayanlar yalnızca kendi erişebildiği bulgulara aksiyon ekleyebilir
            HashSet<int>? allowedFindingIds = null;
            if (module is "audit-actions")
            {
                if (!authSvc.HasPermission(userObj, "audit.close_approve"))
                    allowedFindingIds = auditSvc.GetFindingsForUser(userObj).Select(f => f.Id).ToHashSet();
            }

            ImportResult result = module switch
            {
                "risks"         => importSvc.ImportRisksFromExcel(stream, userObj.Id),
                "controls"      => importSvc.ImportControlsFromExcel(stream, userObj.Id, allowedRiskIds),
                "action-plans"  => importSvc.ImportActionPlansFromExcel(stream, userObj.Id, allowedRiskIds),
                "findings"      => importSvc.ImportFindingsFromExcel(stream, userObj.Id),
                "audit-actions" => importSvc.ImportAuditActionsFromExcel(stream, userObj.Id, allowedFindingIds),
                "ethics"        => importSvc.ImportEthicsFromExcel(stream),
                "library"       => libraryImportSvc.ImportFromExcel(stream, userObj.Id),
                _               => new ImportResult { Errors = ["Geçersiz modül."] }
            };

            return Results.Ok(result);
        }).RequireAuthorization();

        // ── Dosya İndirme: Bulgu Ekleri ──────────────────────────────────────
        app.MapGet("/audit/findings/{findingId:int}/attachments/{attachmentId:int}/download",
            async (int findingId, int attachmentId, AppDbContext db, IWebHostEnvironment env, ClaimsPrincipal user) =>
            {
                try
                {
                    var attachment = await db.FindingAttachments
                        .Include(a => a.Finding).ThenInclude(f => f.InternalAudit)
                        .FirstOrDefaultAsync(a => a.Id == attachmentId && a.FindingId == findingId);
                    if (attachment == null) return Results.NotFound();
                    if (!AuditService.CanDownloadFindingFile(user, attachment.Finding)) return Results.Forbid();

                    var resolved = AuditService.ResolveAttachmentPath(attachment, env.ContentRootPath, env.WebRootPath);
                    return resolved is null
                        ? Results.NotFound()
                        : Results.File(resolved.Value.StoredPath!, "application/octet-stream", resolved.Value.FileName);
                }
                catch (Exception ex)
                {
                    app.Services.GetRequiredService<ILogger<Program>>()
                        .LogError(ex, "Attachment download failed: findingId={FindingId} attachmentId={AttachmentId}", findingId, attachmentId);
                    return Results.Problem("Dosya indirme sırasında hata oluştu.", statusCode: 500);
                }
            })
            .RequireAuthorization();

        // ── Dosya İndirme: Kapanış Dosyaları ─────────────────────────────────
        app.MapGet("/audit/findings/{findingId:int}/closure-files/{fileName}",
            async (int findingId, string fileName, AppDbContext db, IWebHostEnvironment env, ClaimsPrincipal user) =>
            {
                try
                {
                    var finding = await db.AuditFindings
                        .Include(f => f.InternalAudit)
                        .FirstOrDefaultAsync(f => f.Id == findingId);
                    if (finding == null) return Results.NotFound();
                    if (!AuditService.CanDownloadFindingFile(user, finding)) return Results.Forbid();
                    if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
                        return Results.BadRequest();

                    var resolved = AuditService.ResolveClosureFilePath(findingId, fileName, env.ContentRootPath, env.WebRootPath);
                    return resolved is null
                        ? Results.NotFound()
                        : Results.File(resolved.Value.StoredPath!, "application/octet-stream", resolved.Value.FileName);
                }
                catch (Exception ex)
                {
                    app.Services.GetRequiredService<ILogger<Program>>()
                        .LogError(ex, "Closure file download failed: findingId={FindingId} fileName={FileName}", findingId, fileName);
                    return Results.Problem("Dosya indirme sırasında hata oluştu.", statusCode: 500);
                }
            })
            .RequireAuthorization();

        return app;
    }
}
