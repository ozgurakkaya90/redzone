using Microsoft.EntityFrameworkCore;
using System.Threading;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class RiskService(AppDbContext db, IRiskCalculator riskCalculator,
    IHttpContextAccessor? http = null, INotificationService? notifications = null)
{
    // ── Kod üretimi ─────────────────────────────────────────────────────────
    public string GenerateCode()
    {
        var year = DateTime.UtcNow.Year;
        var provider = db.Database.ProviderName ?? "";
        if (provider.Contains("SqlServer"))
        {
            var code = GetNextCodeFromSqlServerSequence(year);
            if (!string.IsNullOrEmpty(code)) return code;
        }
        return $"R-{year}-{CounterHelper.GetNext(db, $"risk-{year}"):D3}";
    }

    private string? GetNextCodeFromSqlServerSequence(int year)
    {
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "EXEC dbo.sp_GetNextRiskCode @year";
            var p = cmd.CreateParameter(); p.ParameterName = "@year"; p.Value = year;
            cmd.Parameters.Add(p);
            return cmd.ExecuteScalar()?.ToString();
        }
        catch { return null; }
    }

    // ── Audit log helper ────────────────────────────────────────────────────
    private string? CurrentIp =>
        http?.HttpContext?.Connection.RemoteIpAddress?.ToString();

    private void Log(int riskId, int? userId, string action,
        string? field = null, string? oldVal = null, string? newVal = null)
    {
        db.RiskAuditLogs.Add(new RiskAuditLog
        {
            RiskId = riskId, UserId = userId,
            Action = action, FieldName = field,
            OldValue = oldVal, NewValue = newVal,
            IpAddress = CurrentIp,
            Timestamp = DateTime.UtcNow
        });
    }

    // ── Query ───────────────────────────────────────────────────────────────
    public IQueryable<Risk> Query() => db.Risks
        .Include(r => r.ProposedBy)
        .Include(r => r.Owner)
        .Include(r => r.Organization).ThenInclude(o => o!.Company)
        .Include(r => r.Department)
        .Include(r => r.Evaluations).ThenInclude(e => e.EvaluatedBy)
        .Include(r => r.Controls).ThenInclude(c => c.EnteredBy)
        .Include(r => r.Controls).ThenInclude(c => c.OwnerDept)
        .Include(r => r.ActionPlans).ThenInclude(a => a.CreatedBy)
        .Include(r => r.ActionPlans).ThenInclude(a => a.OwnerDept)
        .Include(r => r.Reviews).ThenInclude(rv => rv.CreatedBy)
        .Include(r => r.AuditLogs).ThenInclude(l => l.User);

    public Risk? GetById(int id) => Query().FirstOrDefault(r => r.Id == id);
    public async Task<Risk?> GetByIdAsync(int id) => await Query().FirstOrDefaultAsync(r => r.Id == id);

    public Risk? GetByIdForUser(int id, int userId, string role)
    {
        var risk = GetById(id);
        return risk != null && CanAccessRisk(risk, userId, role) ? risk : null;
    }

    public async Task<Risk?> GetByIdForUserAsync(int id, int userId, string role)
    {
        var risk = await GetByIdAsync(id);
        return risk != null && CanAccessRisk(risk, userId, role) ? risk : null;
    }

    public bool CanAccessRisk(Risk risk, int userId, string role)
    {
        if (Roles.RiskManagers.Contains(role)) return true;
        if (db.UserRoles.Any(ur => ur.UserId == userId && Roles.RiskManagers.Contains(ur.RoleName)))
            return true;
        if (risk.ProposedById == userId || risk.OwnerId == userId) return true;

        var userDeptIds = GetUserDepartmentIds(userId);
        if (!risk.DepartmentId.HasValue) return false;
        return userDeptIds.Contains(risk.DepartmentId.Value);
    }

    private HashSet<int> GetUserDepartmentIds(int userId)
    {
        var direct = db.UserDepartments.Where(ud => ud.UserId == userId).Select(ud => ud.DepartmentId).ToHashSet();
        var primary = db.Users.Where(u => u.Id == userId && u.DepartmentId != null).Select(u => u.DepartmentId!.Value).FirstOrDefault();
        if (primary > 0) direct.Add(primary);
        // Müdür olduğu departmanlar
        var managed = db.Departments.Where(d => d.ManagerUserId == userId).Select(d => d.Id).ToHashSet();
        direct.UnionWith(managed);
        return direct;
    }

    private HashSet<int> GetUserOrganizationIds(int userId, HashSet<int>? deptIds = null)
    {
        deptIds ??= GetUserDepartmentIds(userId);
        // Departmanlar üzerinden organizasyonlar
        var fromDepts = db.Departments
            .Where(d => deptIds.Contains(d.Id) && d.OrganizationId != null)
            .Select(d => d.OrganizationId!.Value).ToHashSet();
        // Doğrudan atanan
        var direct = db.UserOrganizations.Where(uo => uo.UserId == userId).Select(uo => uo.OrganizationId).ToHashSet();
        fromDepts.UnionWith(direct);
        return fromDepts;
    }

    public List<Risk> GetAll(string? category = null, string? status = null, string? search = null)
        => [.. BuildFilteredQuery(Query(), category, status, search).OrderByDescending(r => r.ProposedAt)];

    public async Task<List<Risk>> GetAllAsync(string? category = null, string? status = null, string? search = null)
        => await BuildFilteredQuery(Query(), category, status, search).OrderByDescending(r => r.ProposedAt).ToListAsync();

    public List<Risk> GetForUser(int userId, string role,
        string? category = null, string? status = null, string? search = null)
        => [.. BuildUserQuery(userId, role, category, status, search).OrderByDescending(r => r.ProposedAt)];

    public async Task<List<Risk>> GetForUserAsync(int userId, string role,
        string? category = null, string? status = null, string? search = null)
        => await BuildUserQuery(userId, role, category, status, search).OrderByDescending(r => r.ProposedAt).ToListAsync();

    private IQueryable<Risk> BuildFilteredQuery(IQueryable<Risk> q,
        string? category, string? status, string? search)
    {
        if (!string.IsNullOrEmpty(category)) q = q.Where(r => r.Category == category);
        if (!string.IsNullOrEmpty(status))   q = q.Where(r => r.Status == status);
        if (!string.IsNullOrEmpty(search))
            q = q.Where(r => r.Title.Contains(search) || r.Code.Contains(search));
        return q;
    }

    private IQueryable<Risk> BuildUserQuery(int userId, string role,
        string? category, string? status, string? search)
    {
        var q = BuildFilteredQuery(Query(), category, status, search);
        if (!Roles.RiskManagers.Contains(role) &&
            !db.UserRoles.Any(ur => ur.UserId == userId && Roles.RiskManagers.Contains(ur.RoleName)))
        {
            var userDeptIds = GetUserDepartmentIds(userId);
            q = q.Where(r =>
                r.ProposedById == userId || r.OwnerId == userId ||
                (r.DepartmentId != null && userDeptIds.Contains(r.DepartmentId.Value)));
        }
        return q;
    }

    // ── CRUD ────────────────────────────────────────────────────────────────
    public Risk Create(string title, string? description, string? category,
        int? organizationId, string? riskStrategy, int? proposedById, string? proposerName)
    {
        const int maxAttempts = 5;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var risk = new Risk
            {
                Code = GenerateCode(), Title = title, Description = description,
                Category = category, OrganizationId = organizationId,
                RiskStrategy = riskStrategy, ProposedById = proposedById, ProposerName = proposerName,
            };
            db.Risks.Add(risk);
            try
            {
                db.SaveChanges();
                Log(risk.Id, proposedById, "Risk Önerildi", newVal: title);

                if (organizationId.HasValue)
                {
                    var managers = db.Departments
                        .Where(d => d.OrganizationId == organizationId && d.ManagerUserId != null)
                        .Select(d => d.ManagerUserId).Distinct().ToList();
                    foreach (var mId in managers)
                        if (mId != proposedById)
                            Log(risk.Id, mId, "Bildirim", newVal: "Yeni risk önerisi birim müdürüne iletildi.");
                }

                db.SaveChanges();
                _ = notifications?.NotifyRiskProposedAsync(risk.Id, risk.Code, title);
                return risk;
            }
            catch (DbUpdateException)
            {
                try { db.Entry(risk).State = EntityState.Detached; } catch { }
                Thread.Sleep(50 + attempt * 20);
            }
        }
        throw new InvalidOperationException("Benzersiz risk kodu oluşturulamadı.");
    }

    public bool UpdateStatus(int id, string newStatus, string? rejectionReason, User currentUser)
    {
        var risk = db.Risks.Find(id);
        if (risk == null) return false;

        // İş Akışı (Workflow) Geçiş Kuralları
        // "under_review" ve "drafting" eş anlamlı kullanılır — UI "under_review", eski veriler "drafting"
        var allowedTransitions = new Dictionary<string, string[]>
        {
            { "proposed",          new[] { "under_review", "drafting", "rejected" } },
            { "under_review",      new[] { "awaiting_approval", "rejected" } },
            { "drafting",          new[] { "awaiting_approval", "under_review", "rejected" } },
            { "awaiting_approval", new[] { "approved", "under_review", "drafting", "rejected" } },
            { "approved",          new[] { "strategy_set", "controlled", "action_planned" } },
            { "strategy_set",      new[] { "controlled" } },
            { "controlled",        new[] { "residual_evaluated", "action_planned" } },
            { "residual_evaluated",new[] { "action_planned", "risk_accepted" } },
            { "action_planned",    new[] { "controlled", "residual_evaluated" } },
            { "rejected",          new[] { "under_review", "drafting" } }
        };

        if (!allowedTransitions.TryGetValue(risk.Status, out var allowed) || !allowed.Contains(newStatus))
            return false;

        // Rol Bazlı Geçiş Yetkilendirmesi
        if (!currentUser.HasRole(Roles.Admin))
        {
            if (newStatus == RiskStatus.Approved && !currentUser.HasRole(Roles.Committee))
                return false;

            if (newStatus == RiskStatus.AwaitingApproval &&
                !currentUser.HasAnyRole(Roles.RiskOwner, Roles.RiskManager, Roles.AuditManager))
                return false;

            if (risk.Status == RiskStatus.Proposed && newStatus is RiskStatus.UnderReview or RiskStatus.Drafting &&
                !currentUser.HasAnyRole(Roles.RiskManager, Roles.AuditManager) &&
                risk.OwnerId != currentUser.Id)
                return false;
        }

        var oldStatus = risk.Status;
        risk.Status = newStatus;
        if (newStatus == RiskStatus.Rejected) risk.RejectionReason = rejectionReason;
        Log(id, currentUser?.Id, "Durum Değişikliği", "Durum", StatusLabel(oldStatus), StatusLabel(newStatus));
        db.SaveChanges();
        _ = notifications?.NotifyStatusChangedAsync(id, risk.Code, oldStatus, newStatus, risk.OwnerId);
        return true;
    }

    public bool UpdateMetadata(int id, int? organizationId, int? departmentId, string? riskStrategy, int? userId = null)
    {
        var risk = db.Risks.Find(id);
        if (risk == null) return false;
        var orgName  = organizationId.HasValue  ? db.Organizations.Find(organizationId.Value)?.Name  : null;
        var deptName = departmentId.HasValue    ? db.Departments.Find(departmentId.Value)?.Name      : null;
        var oldOrg   = risk.OrganizationId.HasValue ? db.Organizations.Find(risk.OrganizationId.Value)?.Name : null;

        risk.OrganizationId = organizationId;
        risk.DepartmentId   = departmentId;
        if (riskStrategy != null) risk.RiskStrategy = string.IsNullOrEmpty(riskStrategy) ? null : riskStrategy;
        if (risk.Status == "approved" && risk.OrganizationId != null && risk.RiskStrategy != null)
            risk.Status = "strategy_set";

        Log(id, userId, "Sorumluluk & Strateji Güncellendi", "Organizasyon", oldOrg, orgName);
        if (deptName != null)
            Log(id, userId, "Sorumluluk & Strateji Güncellendi", "Departman", null, deptName);
        if (!string.IsNullOrEmpty(riskStrategy))
            Log(id, userId, "Sorumluluk & Strateji Güncellendi", "Strateji", risk.RiskStrategy, riskStrategy);
        db.SaveChanges();
        return true;
    }

    public bool UpdateRiskFields(int id, string sourceType, string? source, string? hazard,
        string? possibleImpact, string? affectedPersons, string? relevantLegislation,
        DateTime? lastReviewedAt, string? lastReviewerName, string? lastReviewerTitle,
        string? currentStatus = null, int? userId = null, string? category = null)
    {
        var risk = db.Risks.Find(id);
        if (risk == null) return false;

        var changes = new List<(string field, string? oldV, string? newV)>();
        void Track(string f, string? o, string? n) { if (o != n) changes.Add((f, o, n)); }

        Track("Kategori", risk.Category, category);
        Track("Kaynak Türü", risk.SourceType, sourceType);
        Track("Kaynak", risk.Source, source);
        Track("Tehlike", risk.Hazard, hazard);
        Track("Olası Etki", risk.PossibleImpact, possibleImpact);
        Track("Etkilenecek Kişiler", risk.AffectedPersons, affectedPersons);
        Track("İlgili Mevzuat", risk.RelevantLegislation, relevantLegislation);
        Track("Mevcut Durum", risk.CurrentStatus, currentStatus);

        if (category != null) risk.Category = string.IsNullOrEmpty(category) ? null : category;
        risk.SourceType = sourceType; risk.Source = source; risk.Hazard = hazard;
        risk.PossibleImpact = possibleImpact; risk.AffectedPersons = affectedPersons;
        risk.RelevantLegislation = relevantLegislation; risk.CurrentStatus = currentStatus;
        risk.LastReviewedAt = lastReviewedAt; risk.LastReviewerName = lastReviewerName;
        risk.LastReviewerTitle = lastReviewerTitle;

        foreach (var (f, o, n) in changes)
            Log(id, userId, "Risk Detayları Güncellendi", f, o, n);
        if (changes.Count == 0)
            Log(id, userId, "Risk Detayları Güncellendi");

        db.SaveChanges();
        return true;
    }

    public bool AssignOwner(int id, int ownerId, User currentUser)
    {
        if (!currentUser.HasAnyRole(Roles.Admin, Roles.RiskManager, Roles.Committee)) return false;
        var risk = db.Risks.Find(id); if (risk == null) return false;
        Log(id, currentUser.Id, "Risk Sahibi Atandı", "OwnerId",
            risk.OwnerId?.ToString(), ownerId.ToString());
        risk.OwnerId = ownerId;
        db.SaveChanges();
        _ = notifications?.NotifyOwnerAssignedAsync(id, risk.Code, risk.Title, ownerId);
        return true;
    }

    // ── Değerlendirme ────────────────────────────────────────────────────────
    public Evaluation AddEvaluation(int riskId, string evalType,
        double probability, double exposure, double consequence, string? notes, int evaluatedById)
    {
        var existing = db.Evaluations.Where(e => e.RiskId == riskId && e.EvalType == evalType).ToList();
        db.Evaluations.RemoveRange(existing);

        var score = Math.Round(probability * exposure * consequence, 2);
        var eval = new Evaluation
        {
            RiskId = riskId, EvalType = evalType,
            Probability = probability, Exposure = exposure, Consequence = consequence,
            Score = score, RiskLevel = riskCalculator.CalculateRiskLevel(score),
            Notes = notes, EvaluatedById = evaluatedById
        };
        db.Evaluations.Add(eval);

        var risk = db.Risks.Find(riskId);
        if (risk != null && evalType == EvalType.Initial)
        {
            if (risk.Status is RiskStatus.Proposed or RiskStatus.Drafting or RiskStatus.UnderReview)
            {
                // Düşük riskler (<70) otomatik onaylanır; yüksek riskler (>=70) incelemeye alınır.
                risk.Status = score < 70 ? RiskStatus.Approved : RiskStatus.UnderReview;
            }
        }

        if (risk != null && evalType == EvalType.Residual && risk.Status == RiskStatus.Controlled)
            risk.Status = RiskStatus.ResidualEvaluated;

        Log(riskId, evaluatedById,
            evalType == "initial" ? "İlk Değerlendirme Yapıldı" : "Kalan Risk Değerlendirmesi Yapıldı",
            "Skor", existing.FirstOrDefault()?.Score.ToString(), score.ToString());
        db.SaveChanges();
        return eval;
    }

    // ── Kontroller ───────────────────────────────────────────────────────────
    public Control AddControl(int riskId, string description, string controlType,
        string? effectiveness, string? frequency, int enteredById, int? ownerDeptId = null)
    {
        var ctrl = new Control
        {
            RiskId = riskId, Description = description, ControlType = controlType,
            Effectiveness = effectiveness, Frequency = frequency,
            EnteredById = enteredById, OwnerDeptId = ownerDeptId > 0 ? ownerDeptId : null,
        };
        db.Controls.Add(ctrl);

        var risk = db.Risks.Find(riskId);
        if (risk != null && risk.Status == RiskStatus.StrategySet) risk.Status = RiskStatus.Controlled;

        Log(riskId, enteredById, "Kontrol Eklendi", "Açıklama", null, description);
        db.SaveChanges();
        return ctrl;
    }

    public bool EditControl(int riskId, int controlId, string description,
        string controlType, string? effectiveness, string? frequency, int? ownerDeptId, int? userId = null)
    {
        var ctrl = db.Controls.FirstOrDefault(c => c.Id == controlId && c.RiskId == riskId);
        if (ctrl == null) return false;

        var changes = new List<string>();
        if (ctrl.Description != description) changes.Add($"Açıklama: \"{ctrl.Description}\" → \"{description}\"");
        if (ctrl.ControlType != controlType) changes.Add($"Tür: {ctrl.ControlType} → {controlType}");

        ctrl.Description = description; ctrl.ControlType = controlType;
        ctrl.Effectiveness = effectiveness; ctrl.Frequency = frequency;
        ctrl.OwnerDeptId = ownerDeptId > 0 ? ownerDeptId : null;

        Log(riskId, userId, "Kontrol Düzenlendi", "Kontrol", null,
            changes.Any() ? string.Join("; ", changes) : "Güncellendi");
        db.SaveChanges();
        return true;
    }

    public bool DeleteControl(int riskId, int controlId, int? userId = null)
    {
        var ctrl = db.Controls.FirstOrDefault(c => c.Id == controlId && c.RiskId == riskId);
        if (ctrl == null) return false;
        Log(riskId, userId, "Kontrol Silindi", "Açıklama", ctrl.Description, null);
        db.Controls.Remove(ctrl);
        db.SaveChanges();
        return true;
    }

    // ── Aksiyon Planları ─────────────────────────────────────────────────────
    public ActionPlan AddAction(int riskId, string description, string responsible,
        DateOnly? dueDate, int createdById, int? ownerDeptId = null)
    {
        var action = new ActionPlan
        {
            RiskId = riskId, Description = description, Responsible = responsible,
            DueDate = dueDate, CreatedById = createdById,
            OwnerDeptId = ownerDeptId > 0 ? ownerDeptId : null,
        };
        db.ActionPlans.Add(action);

        var risk = db.Risks.Find(riskId);
        if (risk != null && risk.Status == RiskStatus.ResidualEvaluated) risk.Status = RiskStatus.ActionPlanned;

        Log(riskId, createdById, "Aksiyon Eklendi", "Açıklama", null, description);
        db.SaveChanges();
        return action;
    }

    public bool EditAction(int riskId, int actionId, string description,
        int? ownerDeptId, DateOnly? dueDate, int? userId = null)
    {
        var action = db.ActionPlans.FirstOrDefault(a => a.Id == actionId && a.RiskId == riskId);
        if (action == null) return false;

        var changes = new List<string>();
        if (action.Description != description) changes.Add($"Açıklama: \"{action.Description}\" → \"{description}\"");
        if (action.DueDate != dueDate) changes.Add($"Hedef Tarih: {action.DueDate} → {dueDate}");

        action.Description = description;
        action.OwnerDeptId = ownerDeptId > 0 ? ownerDeptId : null;
        action.DueDate = dueDate;

        Log(riskId, userId, "Aksiyon Düzenlendi", "Aksiyon", null,
            changes.Any() ? string.Join("; ", changes) : "Güncellendi");
        db.SaveChanges();
        return true;
    }

    public bool UpdateActionStatus(int riskId, int actionId, string newStatus, int? userId = null)
    {
        var action = db.ActionPlans.FirstOrDefault(a => a.Id == actionId && a.RiskId == riskId);
        if (action == null) return false;

        var oldStatus = action.Status;
        action.Status = newStatus;
        if (newStatus == "completed") action.CompletedAt = DateTime.UtcNow;

        Log(riskId, userId, "Aksiyon Durumu Güncellendi", "Durum",
            ActionStatusLabel(oldStatus), ActionStatusLabel(newStatus));

        // Tüm aksiyonlar tamamlandıysa kalan riski yeniden değerlendirmeye gerek var
        db.SaveChanges();

        var allDone = !db.ActionPlans.Any(a => a.RiskId == riskId
            && a.Status != ActionStatus.Completed && a.Status != ActionStatus.Cancelled);
        if (allDone && newStatus == ActionStatus.Completed)
        {
            var risk = db.Risks.Find(riskId);
            if (risk?.Status == RiskStatus.ActionPlanned)
            {
                risk.Status = RiskStatus.ResidualEvaluated;
                Log(riskId, userId, "Tüm Aksiyonlar Tamamlandı — Kalan Risk Yeniden Değerlendirilmeli",
                    "Durum", "Aksiyon Planlandı", "Kalan Risk");
                db.SaveChanges();
            }
        }
        return true;
    }

    public bool DeleteAction(int riskId, int actionId, int? userId = null)
    {
        var action = db.ActionPlans.FirstOrDefault(a => a.Id == actionId && a.RiskId == riskId);
        if (action == null) return false;
        Log(riskId, userId, "Aksiyon Silindi", "Açıklama", action.Description, null);
        db.ActionPlans.Remove(action);
        db.SaveChanges();
        return true;
    }

    // ── Gözden Geçirmeler ────────────────────────────────────────────────────
    public RiskReview AddReview(int riskId, DateTime meetingDate, string? decision,
        string? notes, int createdById)
    {
        var review = new RiskReview
        {
            RiskId = riskId, MeetingDate = meetingDate.ToUniversalTime(),
            Decision = decision, Notes = notes, CreatedById = createdById,
        };
        db.RiskReviews.Add(review);
        Log(riskId, createdById, "Gözden Geçirme Kaydedildi",
            "Toplantı Tarihi", null, meetingDate.ToString("dd.MM.yyyy"));
        if (!string.IsNullOrEmpty(decision))
            Log(riskId, createdById, "Gözden Geçirme Kaydedildi", "Karar", null, decision);
        db.SaveChanges();
        return review;
    }

    public void DeleteReview(int reviewId, int? userId = null)
    {
        var r = db.RiskReviews.Find(reviewId);
        if (r == null) return;
        Log(r.RiskId, userId, "Gözden Geçirme Silindi",
            "Toplantı Tarihi", r.MeetingDate.ToString("dd.MM.yyyy"), null);
        db.RiskReviews.Remove(r);
        db.SaveChanges();
    }

    // ── Önceki / Sonraki navigasyon ──────────────────────────────────────────
    public (int? PrevId, int? NextId) GetAdjacentIds(int riskId, int userId, string role)
    {
        var ids = GetForUser(userId, role)
            .OrderByDescending(r => r.ProposedAt)
            .Select(r => r.Id)
            .ToList();

        var idx = ids.IndexOf(riskId);
        if (idx < 0) return (null, null);

        var prevId = idx > 0            ? ids[idx - 1] : (int?)null;
        var nextId = idx < ids.Count - 1 ? ids[idx + 1] : (int?)null;
        return (prevId, nextId);
    }

    // ── Dashboard / Radar ────────────────────────────────────────────────────
    public List<CategoryRadarData> GetRadarData()
    {
        var risks = Query().ToList();
        return risks.Where(r => r.Category != null).GroupBy(r => r.Category!)
            .Select(g =>
            {
                var inits  = g.SelectMany(r => r.Evaluations.Where(e => e.EvalType == "initial")).ToList();
                var resids = g.SelectMany(r => r.Evaluations.Where(e => e.EvalType == "residual")).ToList();
                return new CategoryRadarData
                {
                    Category = g.Key, Count = g.Count(),
                    AvgInitial = inits.Count > 0 ? Math.Round(inits.Average(e => e.Score), 1) : 0,
                    MaxInitial = inits.Count > 0 ? inits.Max(e => e.Score) : 0,
                    AvgResidual = resids.Count > 0 ? Math.Round(resids.Average(e => e.Score), 1) : 0,
                    MaxResidual = resids.Count > 0 ? resids.Max(e => e.Score) : 0,
                    AvgReduction = inits.Count > 0 && resids.Count > 0
                        ? (int?)Math.Round((1 - resids.Average(e => e.Score) / inits.Average(e => e.Score)) * 100) : null
                };
            })
            .OrderByDescending(d => d.AvgInitial).ToList();
    }

    public List<ActionPlan> GetOverdueActions(int userId, string role)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var q = db.ActionPlans
            .Include(a => a.Risk)
            .Include(a => a.CreatedBy)
            .Include(a => a.OwnerDept)
            .Where(a => a.DueDate.HasValue
                     && a.DueDate < today
                     && a.Status != ActionStatus.Completed
                     && a.Status != ActionStatus.Cancelled);

        if (!Roles.RiskManagers.Contains(role) &&
            !db.UserRoles.Any(ur => ur.UserId == userId && Roles.RiskManagers.Contains(ur.RoleName)))
        {
            var deptIds = GetUserDepartmentIds(userId);
            q = q.Where(a => a.Risk != null && (
                a.Risk.ProposedById == userId ||
                a.Risk.OwnerId == userId ||
                (a.Risk.DepartmentId != null && deptIds.Contains(a.Risk.DepartmentId.Value))));
        }

        return [.. q.OrderBy(a => a.DueDate)];
    }

    public DashboardStats GetDashboardStats()
    {
        var risks = db.Risks.ToList();
        return new DashboardStats
        {
            Total = risks.Count,
            Proposed = risks.Count(r => r.Status == "proposed"),
            Drafting = risks.Count(r => r.Status is "drafting" or "under_review"),
            UnderReview = risks.Count(r => r.Status == "under_review"),
            AwaitingApproval = risks.Count(r => r.Status == "awaiting_approval"),
            Approved = risks.Count(r => r.Status is "approved" or "strategy_set"),
            Rejected = risks.Count(r => r.Status == "rejected"),
            Controlled = risks.Count(r => r.Status == "controlled"),
            ActionPlanned = risks.Count(r => r.Status is "action_planned" or "risk_accepted"),
        };
    }

    private static string StatusLabel(string s) => RiskStatus.Label(s);
    private static string ActionStatusLabel(string s) => ActionStatus.Label(s);
}

public record CategoryRadarData
{
    public string Category { get; init; } = "";
    public int    Count { get; init; }
    public double AvgInitial { get; init; }
    public double MaxInitial { get; init; }
    public double AvgResidual { get; init; }
    public double MaxResidual { get; init; }
    public int?   AvgReduction { get; init; }
}

public record DashboardStats
{
    public int Total, Proposed, Drafting, AwaitingApproval, Approved, Rejected, Controlled, ActionPlanned, UnderReview;
}
