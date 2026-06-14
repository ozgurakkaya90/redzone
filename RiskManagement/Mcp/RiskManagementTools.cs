using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;

namespace RiskManagement.Mcp;

[McpServerToolType]
public class RiskManagementTools(
    AppDbContext db,
    RiskService riskSvc,
    AuditService auditSvc,
    EthicsService ethicsSvc,
    ExternalAuditService extSvc,
    AuthService authSvc,
    McpRequestContext ctx)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Dashboard ────────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_dashboard")]
    [Description("Risk yönetim sisteminin genel durumunu özetler. Toplam risk sayısı, risk seviyesi dağılımı, aksiyon planı istatistikleri ve gecikmiş aksiyonlar hakkında bilgi döndürür.")]
    public string GetDashboard()
    {
        // Servis katmanı üzerinden — yetki kontrolü ve iş mantığı devrede
        var risks = ctx.IsFullAccess
            ? riskSvc.GetAll()
            : riskSvc.GetForUser(ctx.ScopeUser!);

        var today = DateOnly.FromDateTime(DateTime.Today);

        var byStatus = risks.GroupBy(r => r.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var byLevel = risks
            .Select(r => r.Evaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault()?.RiskLevel ?? "değerlendirilmedi")
            .GroupBy(l => l)
            .ToDictionary(g => g.Key, g => g.Count());

        var allActions = risks.SelectMany(r => r.ActionPlans).ToList();
        var overdueCount = allActions.Count(a =>
            a.Status != "completed" && a.Status != "cancelled" &&
            a.DueDate.HasValue && a.DueDate.Value < today);

        var findings = ctx.IsFullAccess
            ? auditSvc.GetFindings()
            : auditSvc.GetFindingsForUser(ctx.ScopeUser!);
        var findingsByStatus = findings.GroupBy(f => f.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new
        {
            risks = new
            {
                total = risks.Count,
                byStatus,
                byRiskLevel = byLevel
            },
            actionPlans = new
            {
                total = allActions.Count,
                overdue = overdueCount,
                planned = allActions.Count(a => a.Status == "planned"),
                inProgress = allActions.Count(a => a.Status == "in_progress"),
                completed = allActions.Count(a => a.Status == "completed"),
            },
            auditFindings = new
            {
                total = findings.Count,
                byStatus = findingsByStatus
            }
        };

        return JsonSerializer.Serialize(result, _json);
    }

    // ── Risk Listesi ─────────────────────────────────────────────────────────

    [McpServerTool(Name = "list_risks")]
    [Description("Riskleri filtreli olarak listeler. Durum (proposed/approved/controlled vb.), risk seviyesi (low/medium/high/critical), kategori veya arama metni ile filtrelenebilir. Her risk için temel bilgiler ve son değerlendirme skoru döndürülür.")]
    public string ListRisks(
        [Description("Risk durumu filtresi: proposed, under_review, approved, strategy_set, controlled, residual_evaluated, action_planned, rejected")] string? status = null,
        [Description("Risk seviyesi filtresi — son değerlendirme RiskLevel alanı")] string? riskLevel = null,
        [Description("Kategori filtresi (örn: 'Operasyonel', 'Mali')")] string? category = null,
        [Description("Başlık veya kodda arama metni")] string? search = null,
        [Description("Maksimum sonuç sayısı (varsayılan: 50)")] int limit = 50)
    {
        // Servis katmanı — filtreleme ve yetki mantığı devrede
        var risks = ctx.IsFullAccess
            ? riskSvc.GetAll(category, status, search).Take(limit).ToList()
            : riskSvc.GetForUser(ctx.ScopeUser!, category, status, search).Take(limit).ToList();

        if (!string.IsNullOrEmpty(riskLevel))
        {
            risks = risks.Where(r =>
            {
                var lastEval = r.Evaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();
                return string.Equals(lastEval?.RiskLevel, riskLevel, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        var result = risks.Select(r =>
        {
            var lastEval = r.Evaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();
            return new
            {
                r.Id,
                r.Code,
                r.Title,
                r.Category,
                r.Status,
                r.SourceType,
                department = r.Department?.Name,
                organization = r.Organization?.Name,
                owner = r.Owner?.FullName,
                proposedAt = r.ProposedAt.ToString("yyyy-MM-dd"),
                latestEvaluation = lastEval == null ? null : new
                {
                    score = lastEval.Score,
                    level = lastEval.RiskLevel,
                    type = lastEval.EvalType,
                    date = lastEval.EvaluatedAt.ToString("yyyy-MM-dd")
                }
            };
        });

        return JsonSerializer.Serialize(result, _json);
    }

    // ── Risk Detayı ──────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_risk_detail")]
    [Description("Belirtilen ID'ye sahip riskin tam detayını döndürür. Tüm değerlendirmeler, kontroller, aksiyon planları ve denetim kayıtları dahildir.")]
    public string GetRiskDetail(
        [Description("Risk ID numarası")] int riskId)
    {
        // Servis Query() — tüm Include'lar devrede, erişim kontrolü mevcut
        var risk = ctx.IsFullAccess
            ? riskSvc.GetById(riskId)
            : riskSvc.GetByIdForUser(riskId, ctx.ScopeUser!);

        if (risk is null)
            return JsonSerializer.Serialize(new { error = "Risk bulunamadı veya erişim yetkiniz yok." }, _json);

        var today = DateOnly.FromDateTime(DateTime.Today);

        var result = new
        {
            risk.Id,
            risk.Code,
            risk.Title,
            risk.Description,
            risk.Category,
            risk.Status,
            risk.SourceType,
            risk.Source,
            risk.Hazard,
            risk.PossibleImpact,
            risk.RelevantLegislation,
            risk.CurrentStatus,
            risk.RiskStrategy,
            risk.RejectionReason,
            department = risk.Department?.Name,
            organization = risk.Organization?.Name,
            owner = risk.Owner?.FullName,
            proposedBy = risk.ProposedBy?.FullName,
            proposedAt = risk.ProposedAt.ToString("yyyy-MM-dd"),
            affectedPersons = risk.GetAffectedPersonsList(),
            evaluations = risk.Evaluations.OrderBy(e => e.EvaluatedAt).Select(e => new
            {
                e.Id,
                e.EvalType,
                e.Probability,
                e.Exposure,
                e.Consequence,
                e.Score,
                e.RiskLevel,
                evaluatedBy = e.EvaluatedBy.FullName,
                evaluatedAt = e.EvaluatedAt.ToString("yyyy-MM-dd"),
                e.Notes
            }),
            controls = risk.Controls.Select(c => new
            {
                c.Id,
                c.Description,
                c.ControlType,
                c.Effectiveness,
                c.Frequency,
                enteredBy = c.EnteredBy.FullName,
                ownerDept = c.OwnerDept?.Name,
                enteredAt = c.EnteredAt.ToString("yyyy-MM-dd")
            }),
            actionPlans = risk.ActionPlans.Select(a => new
            {
                a.Id,
                a.Description,
                a.Responsible,
                ownerDept = a.OwnerDept?.Name,
                dueDate = a.DueDate?.ToString("yyyy-MM-dd"),
                a.Status,
                isOverdue = a.Status != "completed" && a.Status != "cancelled" &&
                            a.DueDate.HasValue && a.DueDate.Value < today,
                a.DelayReason,
                a.ClosureNote,
                createdBy = a.CreatedBy.FullName,
                createdAt = a.CreatedAt.ToString("yyyy-MM-dd"),
                completedAt = a.CompletedAt?.ToString("yyyy-MM-dd")
            }),
            reviews = risk.Reviews.OrderByDescending(rv => rv.MeetingDate).Select(rv => new
            {
                rv.Id,
                meetingDate = rv.MeetingDate.ToString("yyyy-MM-dd"),
                rv.Decision,
                rv.Notes,
                createdBy = rv.CreatedBy.FullName
            })
        };

        return JsonSerializer.Serialize(result, _json);
    }

    // ── Aksiyon Planları ─────────────────────────────────────────────────────

    [McpServerTool(Name = "list_action_plans")]
    [Description("Aksiyon planlarını listeler. Gecikmiş, devam eden veya tamamlanmış aksiyonları filtreler. Risk bazlı veya tüm aksiyonlar görüntülenebilir.")]
    public string ListActionPlans(
        [Description("Durum filtresi: planned, in_progress, completed, cancelled")] string? status = null,
        [Description("Sadece gecikmiş aksiyonları getir")] bool overdueOnly = false,
        [Description("Belirli bir risk ID'sine ait aksiyonlar")] int? riskId = null,
        [Description("Sorumlu kişi adında arama")] string? responsible = null,
        [Description("Maksimum sonuç sayısı (varsayılan: 50)")] int limit = 50)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // ActionPlan için doğrudan DB sorgusu — servis katmanında karşılık yok
        var query = db.ActionPlans.AsNoTracking()
            .Include(a => a.Risk)
            .Include(a => a.CreatedBy)
            .Include(a => a.OwnerDept)
            .AsQueryable();

        // Scoped key'ler yalnızca erişebildikleri risklere ait aksiyonları görebilir
        if (!ctx.IsFullAccess)
        {
            var accessibleRiskIds = riskSvc
                .GetForUser(ctx.ScopeUser!)
                .Select(r => r.Id)
                .ToHashSet();
            query = query.Where(a => accessibleRiskIds.Contains(a.RiskId));
        }

        if (riskId.HasValue)
            query = query.Where(a => a.RiskId == riskId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(a => a.Status == status);
        if (!string.IsNullOrEmpty(responsible))
            query = query.Where(a => a.Responsible.Contains(responsible));
        if (overdueOnly)
            query = query.Where(a =>
                a.Status != "completed" && a.Status != "cancelled" &&
                a.DueDate.HasValue && a.DueDate.Value < today);

        var actions = query.OrderBy(a => a.DueDate).Take(limit).ToList();

        var result = actions.Select(a => new
        {
            a.Id,
            risk = new { a.Risk.Id, a.Risk.Code, a.Risk.Title },
            a.Description,
            a.Responsible,
            ownerDept = a.OwnerDept?.Name,
            dueDate = a.DueDate?.ToString("yyyy-MM-dd"),
            a.Status,
            isOverdue = a.Status != "completed" && a.Status != "cancelled" &&
                        a.DueDate.HasValue && a.DueDate.Value < today,
            daysOverdue = (a.Status != "completed" && a.Status != "cancelled" &&
                           a.DueDate.HasValue && a.DueDate.Value < today)
                ? (int)(today.ToDateTime(TimeOnly.MinValue) - a.DueDate!.Value.ToDateTime(TimeOnly.MinValue)).TotalDays
                : (int?)null,
            a.DelayReason,
            a.ClosureNote,
            createdBy = a.CreatedBy.FullName,
            completedAt = a.CompletedAt?.ToString("yyyy-MM-dd")
        });

        return JsonSerializer.Serialize(result, _json);
    }

    // ── Denetim Bulguları ────────────────────────────────────────────────────

    [McpServerTool(Name = "list_findings")]
    [Description("Denetim bulgularını listeler. Açık, kapalı, gecikmiş bulgular filtrelenebilir. Her bulgu için öncelik, kategori ve aksiyon durumu döndürülür.")]
    public string ListFindings(
        [Description("Durum filtresi: open, closure_requested, closed")] string? status = null,
        [Description("Ciddiyet filtresi")] string? severity = null,
        [Description("Kategori filtresi")] string? category = null,
        [Description("Başlık veya açıklamada arama")] string? search = null,
        [Description("Maksimum sonuç sayısı (varsayılan: 50)")] int limit = 50)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Servis katmanı — filtreleme ve yetki mantığı devrede
        var findings = ctx.IsFullAccess
            ? auditSvc.GetFindings(category, severity, status)
            : auditSvc.GetFindingsForUser(ctx.ScopeUser!, category, severity, status);

        if (!string.IsNullOrEmpty(search))
            findings = findings.Where(f =>
                f.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (f.Description != null && f.Description.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();

        var result = findings.Take(limit).Select(f => new
        {
            f.Id,
            f.Code,
            f.Title,
            f.Description,
            f.Category,
            f.Severity,
            f.Status,
            dueDate = f.DueDate?.ToString("yyyy-MM-dd"),
            isOverdue = f.Status != "closed" && f.DueDate.HasValue && f.DueDate.Value < today,
            department = f.Department?.Name,
            owner = f.Owner?.FullName,
            auditor = f.Auditor.FullName,
            actionsTotal = f.Actions.Count,
            actionsCompleted = f.Actions.Count(a => a.Status == "completed"),
            createdAt = f.CreatedAt.ToString("yyyy-MM-dd")
        });

        return JsonSerializer.Serialize(result, _json);
    }

    // ── Arama ────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "search")]
    [Description("Riskler, iç/dış denetim bulguları ve etik bildirimlerinde tam metin arama yapar.")]
    public string Search(
        [Description("Aranacak metin")] string query,
        [Description("Arama kapsamı: 'risks', 'findings', 'external_audits', 'ethics' veya 'all' (varsayılan: 'all')")] string scope = "all",
        [Description("Maksimum sonuç sayısı (varsayılan: 20)")] int limit = 20)
    {
        var results = new Dictionary<string, object>();

        if (scope is "risks" or "all")
        {
            // Servis katmanı üzerinden arama
            var risks = ctx.IsFullAccess
                ? riskSvc.GetAll(search: query).Take(limit).ToList()
                : riskSvc.GetForUser(ctx.ScopeUser!, search: query).Take(limit).ToList();

            results["risks"] = risks.Select(r =>
            {
                var lastEval = r.Evaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault();
                return new { r.Id, r.Code, r.Title, r.Category, r.Status, riskLevel = lastEval?.RiskLevel };
            });
        }

        if (scope is "findings" or "all")
        {
            var findings = ctx.IsFullAccess
                ? auditSvc.GetFindings()
                : auditSvc.GetFindingsForUser(ctx.ScopeUser!);

            findings = findings.Where(f =>
                f.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (f.Description != null && f.Description.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (f.Category != null && f.Category.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                f.Code.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(limit).ToList();

            results["findings"] = findings.Select(f => new
            {
                f.Id, f.Code, f.Title, f.Category, f.Severity, f.Status
            });
        }

        if (scope is "external_audits" or "all")
        {
            List<ExternalAudit> exAudits;
            if (ctx.IsFullAccess)
            {
                exAudits = db.ExternalAudits.AsNoTracking()
                    .Where(a => a.Subject.Contains(query) ||
                                a.AuditingBody.Contains(query) ||
                                (a.Standard != null && a.Standard.Contains(query)) ||
                                (a.Notes != null && a.Notes.Contains(query)) ||
                                a.Code.Contains(query))
                    .Take(limit).ToList();
            }
            else
            {
                exAudits = extSvc.List(ctx.ScopeUser!, null, null)
                    .Where(a => a.Subject.Contains(query) ||
                                a.AuditingBody.Contains(query) ||
                                (a.Standard != null && a.Standard.Contains(query)) ||
                                (a.Notes != null && a.Notes.Contains(query)) ||
                                a.Code.Contains(query))
                    .Take(limit).ToList();
            }

            results["external_audits"] = exAudits.Select(a => new
            {
                a.Id, a.Code, a.AuditingBody, a.Standard, a.Subject,
                auditDate = a.AuditDate.ToString("yyyy-MM-dd"), a.Status
            });
        }

        if (scope is "ethics" or "all")
        {
            // Scoped key'ler ethics raporlarına erişemez — anonimlik ve veri izolasyonu
            if (ctx.IsFullAccess)
            {
                var ethics = ethicsSvc.GetAll()
                    .Where(r => (r.ReportCategory != null && r.ReportCategory.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                                r.Code.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(limit).ToList();

                results["ethics"] = ethics.Select(r => new
                {
                    r.Id, r.Code, r.ReportCategory, r.Status,
                    submittedAt = r.SubmittedAt.ToString("yyyy-MM-dd")
                });
            }
            else
            {
                results["ethics"] = Array.Empty<object>();
            }
        }

        return JsonSerializer.Serialize(results, _json);
    }

    // ── Risk İstatistikleri ──────────────────────────────────────────────────

    [McpServerTool(Name = "get_risk_statistics")]
    [Description("Detaylı risk istatistikleri döndürür. Kategori bazlı dağılım, departman bazlı risk sayıları ve risk seviyesi trend analizi içerir.")]
    public string GetRiskStatistics()
    {
        // Servis katmanı üzerinden — Query() tüm include'ları yükler
        var risks = ctx.IsFullAccess
            ? riskSvc.GetAll()
            : riskSvc.GetForUser(ctx.ScopeUser!);

        var byCategory = risks
            .Where(r => !string.IsNullOrEmpty(r.Category))
            .GroupBy(r => r.Category!)
            .Select(g =>
            {
                var evals = g.SelectMany(r => r.Evaluations).ToList();
                return new
                {
                    category = g.Key,
                    count = g.Count(),
                    avgScore = evals.Any() ? evals.Average(e => e.Score) : 0,
                    highOrCritical = g.Count(r =>
                    {
                        var level = r.Evaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault()?.RiskLevel;
                        return level is "high" or "critical";
                    })
                };
            })
            .OrderByDescending(x => x.count);

        var byDepartment = risks
            .Where(r => r.Department != null)
            .GroupBy(r => r.Department!.Name)
            .Select(g => new
            {
                department = g.Key,
                count = g.Count(),
                highOrCritical = g.Count(r =>
                {
                    var level = r.Evaluations.OrderByDescending(e => e.EvaluatedAt).FirstOrDefault()?.RiskLevel;
                    return level is "high" or "critical";
                })
            })
            .OrderByDescending(x => x.count);

        var withEval = risks.Where(r => r.Evaluations.Any()).ToList();
        var withoutEval = risks.Count(r => !r.Evaluations.Any());

        var result = new
        {
            total = risks.Count,
            withoutEvaluation = withoutEval,
            byCategory,
            byDepartment,
            scoreDistribution = withEval.Any() ? new
            {
                min = withEval.SelectMany(r => r.Evaluations).Min(e => e.Score),
                max = withEval.SelectMany(r => r.Evaluations).Max(e => e.Score),
                avg = withEval.SelectMany(r => r.Evaluations).Average(e => e.Score)
            } : null
        };

        return JsonSerializer.Serialize(result, _json);
    }

    // ── Dış Denetimler ──────────────────────────────────────────────────────

    [McpServerTool(Name = "list_external_audits")]
    [Description("Dış denetimleri listeler. BRC, JCI, TSE gibi denetleyici kurum, durum (planned/in_progress/completed/closed) ve tarih aralığı ile filtrelenebilir.")]
    public string ListExternalAudits(
        [Description("Denetleyici kurum filtresi (örn: 'BRC', 'JCI', 'TSE')")] string? auditingBody = null,
        [Description("Durum filtresi: planned, in_progress, completed, closed")] string? status = null,
        [Description("Bu yıldan itibaren filtrele (örn: 2024)")] int? fromYear = null,
        [Description("Maksimum sonuç sayısı (varsayılan: 50)")] int limit = 50)
    {
        List<ExternalAudit> audits;
        if (ctx.IsFullAccess)
        {
            // ExternalAuditService admin erişim metodu bulunmuyor — doğrudan sorgu kullanılır
            var query = db.ExternalAudits.AsNoTracking()
                .Include(a => a.ResponsibleDept).Include(a => a.ResponsibleUser).Include(a => a.Findings)
                .AsQueryable();
            if (!string.IsNullOrEmpty(auditingBody)) query = query.Where(a => a.AuditingBody.Contains(auditingBody));
            if (!string.IsNullOrEmpty(status))       query = query.Where(a => a.Status == status);
            if (fromYear.HasValue)                   query = query.Where(a => a.AuditDate.Year >= fromYear.Value);
            audits = query.OrderByDescending(a => a.AuditDate).Take(limit).ToList();
        }
        else
        {
            audits = extSvc.List(ctx.ScopeUser!, auditingBody, status)
                           .Where(a => !fromYear.HasValue || a.AuditDate.Year >= fromYear.Value)
                           .Take(limit).ToList();
        }

        var result = audits.Select(a => new
        {
            a.Id,
            a.Code,
            a.AuditingBody,
            a.Standard,
            a.Subject,
            auditDate = a.AuditDate.ToString("yyyy-MM-dd"),
            endDate = a.EndDate?.ToString("yyyy-MM-dd"),
            a.Status,
            a.Score,
            responsibleDept = a.ResponsibleDept?.Name,
            responsibleUser = a.ResponsibleUser?.FullName,
            findingsTotal = a.Findings.Count,
            openFindings = a.Findings.Count(f => f.Status != "closed"),
            a.Notes
        });

        return JsonSerializer.Serialize(result, _json);
    }

    [McpServerTool(Name = "get_external_audit_detail")]
    [Description("Belirtilen dış denetimin tam detayını ve bulgularını döndürür.")]
    public string GetExternalAuditDetail(
        [Description("Dış denetim ID numarası")] int auditId)
    {
        ExternalAudit? audit;
        if (ctx.IsFullAccess)
        {
            audit = db.ExternalAudits.AsNoTracking()
                .Include(a => a.ResponsibleDept)
                .Include(a => a.ResponsibleUser)
                .Include(a => a.CreatedBy)
                .Include(a => a.Findings).ThenInclude(f => f.Owner)
                .Include(a => a.Findings).ThenInclude(f => f.Department)
                .Include(a => a.Findings).ThenInclude(f => f.Actions)
                .FirstOrDefault(a => a.Id == auditId);
        }
        else
        {
            var bare = extSvc.Get(auditId, ctx.ScopeUser!);
            if (bare is null) { audit = null; }
            else
            {
                audit = db.ExternalAudits.AsNoTracking()
                    .Include(a => a.ResponsibleDept)
                    .Include(a => a.ResponsibleUser)
                    .Include(a => a.CreatedBy)
                    .Include(a => a.Findings).ThenInclude(f => f.Owner)
                    .Include(a => a.Findings).ThenInclude(f => f.Department)
                    .Include(a => a.Findings).ThenInclude(f => f.Actions)
                    .FirstOrDefault(a => a.Id == auditId);
            }
        }

        if (audit is null)
            return JsonSerializer.Serialize(new { error = "Dış denetim bulunamadı veya erişim yetkiniz yok." }, _json);

        var today = DateOnly.FromDateTime(DateTime.Today);

        var result = new
        {
            audit.Id,
            audit.Code,
            audit.AuditingBody,
            audit.Standard,
            audit.Subject,
            auditDate = audit.AuditDate.ToString("yyyy-MM-dd"),
            endDate = audit.EndDate?.ToString("yyyy-MM-dd"),
            audit.Status,
            audit.Score,
            audit.Notes,
            responsibleDept = audit.ResponsibleDept?.Name,
            responsibleUser = audit.ResponsibleUser?.FullName,
            createdBy = audit.CreatedBy.FullName,
            createdAt = audit.CreatedAt.ToString("yyyy-MM-dd"),
            findings = audit.Findings.Select(f => new
            {
                f.Id,
                f.Code,
                f.Title,
                f.Description,
                f.Category,
                f.Severity,
                f.Status,
                dueDate = f.DueDate?.ToString("yyyy-MM-dd"),
                isOverdue = f.Status != "closed" && f.DueDate.HasValue && f.DueDate.Value < today,
                department = f.Department?.Name,
                owner = f.Owner?.FullName,
                actionsTotal = f.Actions.Count,
                actionsCompleted = f.Actions.Count(a => a.Status == "completed")
            })
        };

        return JsonSerializer.Serialize(result, _json);
    }

    // ── Etik Bildirimleri ────────────────────────────────────────────────────

    [McpServerTool(Name = "list_ethics_reports")]
    [Description("Etik bildirimlerini listeler. Durum (pending/audit_reviewed/ethics_reviewed/closed) ve kategori ile filtrelenebilir. Bildirim içeriği (kişisel veri) döndürülmez — sadece özet istatistik ve durum bilgisi.")]
    public string ListEthicsReports(
        [Description("Durum filtresi: pending, audit_reviewed, ethics_reviewed, closed")] string? status = null,
        [Description("Kategori filtresi")] string? category = null,
        [Description("Maksimum sonuç sayısı (varsayılan: 50)")] int limit = 50)
    {
        if (!ctx.IsFullAccess && (ctx.ScopeUser is null || !authSvc.HasPermission(ctx.ScopeUser, "ethics.read")))
            return JsonSerializer.Serialize(new { error = "Bu API anahtarının etik bildirimlerine erişim yetkisi yok." }, _json);

        // Servis katmanı üzerinden
        var reports = ethicsSvc.GetAll(status);

        if (!string.IsNullOrEmpty(category))
            reports = reports.Where(r => r.ReportCategory != null &&
                                         r.ReportCategory.Contains(category, StringComparison.OrdinalIgnoreCase)).ToList();

        reports = reports.Take(limit).ToList();

        var result = new
        {
            total = reports.Count,
            byStatus = reports.GroupBy(r => r.Status)
                .ToDictionary(g => g.Key, g => g.Count()),
            items = reports.Select(r => new
            {
                r.Id,
                r.Code,
                r.ReportCategory,
                r.Status,
                submittedAt = r.SubmittedAt.ToString("yyyy-MM-dd"),
                auditDecision = r.AuditDecision,
                auditReviewedAt = r.AuditReviewedAt?.ToString("yyyy-MM-dd"),
                ethicsDecision = r.EthicsDecision,
                ethicsReviewedAt = r.EthicsReviewedAt?.ToString("yyyy-MM-dd")
            })
        };

        return JsonSerializer.Serialize(result, _json);
    }

    [McpServerTool(Name = "get_ethics_summary")]
    [Description("Etik bildirimleri istatistiklerini döndürür. Toplam sayı, durum dağılımı, kategori bazlı özet ve ortalama inceleme süresi. Kişisel veri içermez.")]
    public string GetEthicsSummary()
    {
        if (!ctx.IsFullAccess && (ctx.ScopeUser is null || !authSvc.HasPermission(ctx.ScopeUser, "ethics.read")))
            return JsonSerializer.Serialize(new { error = "Bu API anahtarının etik bildirimlerine erişim yetkisi yok." }, _json);

        // Servis katmanı üzerinden
        var reports = ethicsSvc.GetAll();

        var reviewed = reports.Where(r => r.AuditReviewedAt.HasValue).ToList();
        var avgReviewDays = reviewed.Any()
            ? reviewed.Average(r => (r.AuditReviewedAt!.Value - r.SubmittedAt).TotalDays)
            : (double?)null;

        var byCategory = reports
            .Where(r => !string.IsNullOrEmpty(r.ReportCategory))
            .GroupBy(r => r.ReportCategory!)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new
        {
            total = reports.Count,
            byStatus = reports.GroupBy(r => r.Status)
                .ToDictionary(g => g.Key, g => g.Count()),
            byCategory,
            pendingAuditReview = reports.Count(r => r.Status == EthicsStatus.Pending),
            pendingEthicsReview = reports.Count(r => r.Status == EthicsStatus.EthicsBoardNotified),
            avgAuditReviewDays = avgReviewDays.HasValue ? Math.Round(avgReviewDays.Value, 1) : (double?)null,
            thisYear = reports.Count(r => r.SubmittedAt.Year == DateTime.UtcNow.Year),
            lastMonth = reports.Count(r => r.SubmittedAt >= DateTime.UtcNow.AddDays(-30))
        };

        return JsonSerializer.Serialize(result, _json);
    }
}
