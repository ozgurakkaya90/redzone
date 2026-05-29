using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class RiskService(AppDbContext db, IRiskCalculator riskCalculator, AuthService authSvc,
    IHttpContextAccessor? http = null, INotificationService? notifications = null,
    ILogger<RiskService>? logger = null, ConfigService? config = null)
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
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "SQL Server sequence sorgusu başarısız (year={Year}), CounterHelper'a düşülüyor", year);
            return null;
        }
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

    /// <summary>
    /// Tam sorgu — detay sayfası için tüm navigasyon property'leri yükler.
    /// Liste/export sayfaları QueryList() kullanmalıdır.
    /// </summary>
    public IQueryable<Risk> Query() => db.Risks
        .AsNoTracking()
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
        .Include(r => r.AuditLogs).ThenInclude(l => l.User)
        .Include(r => r.FindingLinks).ThenInclude(l => l.Finding);

    /// <summary>
    /// Hafif liste sorgusu — kart/tablo görünümü ve export için yeterli (4 Include).
    /// Controls/ActionPlans/Reviews/AuditLogs/FindingLinks gerektiğinde Query() kullanın.
    /// </summary>
    public IQueryable<Risk> QueryList() => db.Risks
        .AsNoTracking()
        .Include(r => r.ProposedBy)
        .Include(r => r.Owner)
        .Include(r => r.Organization).ThenInclude(o => o!.Company)
        .Include(r => r.Department)
        .Include(r => r.Evaluations);

    public Risk? GetById(int id) => Query().FirstOrDefault(r => r.Id == id);
    public async Task<Risk?> GetByIdAsync(int id) => await Query().FirstOrDefaultAsync(r => r.Id == id);

    public Risk? GetByIdForUser(int id, int userId, string role)
    {
        var risk = GetById(id);
        return risk != null && CanAccessRisk(risk, userId, role) ? risk : null;
    }

    public Risk? GetByIdForUser(int id, User user)
    {
        var risk = GetById(id);
        return risk != null && CanAccessRiskForUser(risk, user) ? risk : null;
    }

    public async Task<Risk?> GetByIdForUserAsync(int id, int userId, string role)
    {
        var risk = await GetByIdAsync(id);
        return risk != null && CanAccessRisk(risk, userId, role) ? risk : null;
    }

    public async Task<Risk?> GetByIdForUserAsync(int id, User user)
    {
        var risk = await GetByIdAsync(id);
        return risk != null && CanAccessRiskForUser(risk, user) ? risk : null;
    }

    // risk.manage = tüm risklere erişim (Admin, Committee, RiskManager, AuditManager)
    private bool IsRiskManager(int userId, string role)
    {
        var user = db.Users.Include(u => u.UserRoles).FirstOrDefault(u => u.Id == userId);
        if (user != null) return authSvc.HasPermission(user, "risk.manage");
        // Kullanıcı DB'de yoksa primary rol üzerinden DefaultPermissions'a düş
        return AuthService.DefaultPermissions.TryGetValue(role, out var perms)
               && perms.Contains("risk.manage");
    }

    public bool CanAccessRisk(Risk risk, int userId, string role)
    {
        if (IsRiskManager(userId, role)) return true;
        if (risk.ProposedById == userId || risk.OwnerId == userId) return true;

        var userDeptIds = GetUserDepartmentIds(userId);
        if (!risk.DepartmentId.HasValue) return false;
        return userDeptIds.Contains(risk.DepartmentId.Value);
    }

    public bool CanAccessRiskForUser(Risk risk, User user)
    {
        if (authSvc.HasPermission(user, "risk.manage")) return true;
        if (risk.ProposedById == user.Id || risk.OwnerId == user.Id) return true;
        if (!risk.DepartmentId.HasValue) return false;
        return user.AllDepartmentIds.Contains(risk.DepartmentId.Value);
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

    // Liste metotları — hafif QueryList() kullanır (Controls/Reviews/Logs yüklenmez).
    public List<Risk> GetAll(string? category = null, string? status = null, string? search = null)
        => [.. BuildFilteredQuery(QueryList(), category, status, search).OrderByDescending(r => r.ProposedAt)];

    public async Task<List<Risk>> GetAllAsync(string? category = null, string? status = null, string? search = null)
        => await BuildFilteredQuery(QueryList(), category, status, search).OrderByDescending(r => r.ProposedAt).ToListAsync();

    public List<Risk> GetForUser(int userId, string role,
        string? category = null, string? status = null, string? search = null)
        => [.. BuildUserQuery(userId, role, category, status, search).OrderByDescending(r => r.ProposedAt)];

    public async Task<List<Risk>> GetForUserAsync(int userId, string role,
        string? category = null, string? status = null, string? search = null)
        => await BuildUserQuery(userId, role, category, status, search).OrderByDescending(r => r.ProposedAt).ToListAsync();

    // Tüm rol ve departman atamalarını kullanan overload (DB'den tam yüklü User için)
    public List<Risk> GetForUser(User user,
        string? category = null, string? status = null, string? search = null)
        => [.. BuildUserQueryForUser(user, category, status, search).OrderByDescending(r => r.ProposedAt)];

    public async Task<List<Risk>> GetForUserAsync(User user,
        string? category = null, string? status = null, string? search = null)
        => await BuildUserQueryForUser(user, category, status, search).OrderByDescending(r => r.ProposedAt).ToListAsync();

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
        var q = BuildFilteredQuery(QueryList(), category, status, search);
        if (!IsRiskManager(userId, role))
        {
            var userDeptIds = GetUserDepartmentIds(userId);
            q = q.Where(r =>
                r.ProposedById == userId || r.OwnerId == userId ||
                (r.DepartmentId != null && userDeptIds.Contains(r.DepartmentId.Value)));
        }
        return q;
    }

    private IQueryable<Risk> BuildUserQueryForUser(User user,
        string? category, string? status, string? search)
    {
        var q = BuildFilteredQuery(QueryList(), category, status, search);
        // risk.manage = tüm risklere erişim (admin, committee, risk_manager, audit_manager).
        // Önceden audit.read da eklenmişti; bu denetçiye tüm riskleri açıyor ama
        // CanAccessRisk/CanAccessRiskForUser bunu onaylamıyor ve detay sayfası erişimi
        // reddediyordu — liste ≠ detay tutarsızlığı. Yalnızca risk.manage kalıyor.
        if (!authSvc.HasPermission(user, "risk.manage"))
        {
            var deptIds = user.AllDepartmentIds.ToHashSet();
            q = q.Where(r =>
                r.ProposedById == user.Id || r.OwnerId == user.Id ||
                (r.DepartmentId != null && deptIds.Contains(r.DepartmentId.Value)));
        }
        return q;
    }

    // ── CRUD ────────────────────────────────────────────────────────────────
    public async Task<Risk> CreateAsync(string title, string? description, string? category,
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
                await using var tx = await db.Database.BeginTransactionAsync();
                await db.SaveChangesAsync();

                Log(risk.Id, proposedById, "Risk Önerildi", newVal: title);

                if (organizationId.HasValue)
                {
                    var managers = await db.Departments
                        .Where(d => d.OrganizationId == organizationId && d.ManagerUserId != null)
                        .Select(d => d.ManagerUserId).Distinct().ToListAsync();
                    foreach (var mId in managers)
                        if (mId != proposedById)
                            Log(risk.Id, mId, "Bildirim", newVal: "Yeni risk önerisi birim müdürüne iletildi.");
                }

                await db.SaveChangesAsync();
                await tx.CommitAsync();

                try
                {
                    if (notifications != null)
                        await notifications.NotifyRiskProposedAsync(risk.Id, risk.Code, title);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Risk önerisi bildirimi gönderilemedi: {Code}", risk.Code);
                }

                return risk;
            }
            catch (DbUpdateException ex)
            {
                logger?.LogWarning(ex, "Risk kodu çakışması (deneme {Attempt}/{Max}), yeniden deneniyor", attempt + 1, maxAttempts);
                // Başarısız entity'lerin bir sonraki denemeye sızmaması için ChangeTracker temizlenir.
                // Sadece risk'i detach etmek yetmez; transaction rollback'i sonrası log entry'leri
                // de ChangeTracker'da kalabilir ve FK ihlali yaratır.
                db.ChangeTracker.Clear();
            }
        }
        throw new InvalidOperationException("Benzersiz risk kodu oluşturulamadı.");
    }

    /// <summary>Geriye dönük uyumluluk — yeni kodu CreateAsync ile yazın.</summary>
    [Obsolete("Blazor Server'da deadlock riski. CreateAsync kullanın.", error: false)]
    public Risk Create(string title, string? description, string? category,
        int? organizationId, string? riskStrategy, int? proposedById, string? proposerName)
        => CreateAsync(title, description, category, organizationId, riskStrategy, proposedById, proposerName)
            .GetAwaiter().GetResult();

    public async Task<bool> UpdateStatusAsync(int id, string newStatus, string? rejectionReason, User currentUser)
    {
        var risk = await db.Risks.FindAsync(id);
        if (risk == null) return false;

        // İş akışı geçiş kontrolü — tek yetkili kaynak RiskWorkflow
        if (!RiskWorkflow.CanTransition(risk.Status, newStatus))
            return false;

        // İzin Bazlı Geçiş Yetkilendirmesi — reddler güvenlik denetimi için yapısal loglanır.
        if (newStatus == RiskStatus.Approved && !authSvc.HasPermission(currentUser, "risk.approve"))
        {
            logger?.LogWarning("Risk durum geçişi reddi — RiskId: {RiskId}, Hedef: {Status}, Kullanıcı: {User} (yetki: risk.approve yok)",
                id, newStatus, currentUser?.Username);
            return false;
        }

        if (newStatus == RiskStatus.AwaitingApproval && !authSvc.HasPermission(currentUser, "risk.modify"))
        {
            logger?.LogWarning("Risk durum geçişi reddi — RiskId: {RiskId}, Hedef: {Status}, Kullanıcı: {User} (yetki: risk.modify yok)",
                id, newStatus, currentUser?.Username);
            return false;
        }

        if (risk.Status == RiskStatus.Proposed && newStatus == RiskStatus.UnderReview &&
            !authSvc.HasPermission(currentUser, "risk.initiate_review") && risk.OwnerId != currentUser.Id)
        {
            logger?.LogWarning("Risk durum geçişi reddi — RiskId: {RiskId}, Hedef: {Status}, Kullanıcı: {User} (yetki: risk.initiate_review yok ve sahip değil)",
                id, newStatus, currentUser?.Username);
            return false;
        }

        var oldStatus = risk.Status;
        risk.Status = newStatus;
        if (newStatus == RiskStatus.Rejected) risk.RejectionReason = rejectionReason;
        Log(id, currentUser?.Id, "Durum Değişikliği", "Durum", StatusLabel(oldStatus), StatusLabel(newStatus));
        await db.SaveChangesAsync();
        try
        {
            if (notifications != null)
                await notifications.NotifyStatusChangedAsync(id, risk.Code, oldStatus, newStatus, risk.OwnerId);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Durum değişikliği bildirimi gönderilemedi: {Code}", risk.Code);
        }
        return true;
    }

    [Obsolete("Blazor Server'da deadlock riski. UpdateStatusAsync kullanın.", error: false)]
    public bool UpdateStatus(int id, string newStatus, string? rejectionReason, User currentUser)
        => UpdateStatusAsync(id, newStatus, rejectionReason, currentUser).GetAwaiter().GetResult();

    /// <summary>Kalıntı riski kabul eder; iş akışı geçişini ve loglama dahil tüm mantığı içerir.</summary>
    public async Task<(bool Ok, string? Error)> AcceptRiskAsync(int riskId, string? reason, int userId)
    {
        var risk = await db.Risks.FindAsync(riskId);
        if (risk == null) return (false, "Risk bulunamadı.");

        if (!RiskWorkflow.CanTransition(risk.Status, RiskStatus.RiskAccepted))
            return (false, "Mevcut risk durumunda kalıntı risk kabul edilemez.");

        var oldStatus = risk.Status;           // geçiş öncesi durum — bildirimde kullanılacak
        risk.Status = RiskStatus.RiskAccepted;
        // Gerekçe; semantik olarak AcceptanceReason — depolama alanı RejectionReason'ı paylaşır.
        risk.RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        Log(riskId, userId > 0 ? userId : null, "Kalıntı Risk Kabul Edildi",
            newVal: risk.RejectionReason);

        await db.SaveChangesAsync();

        try
        {
            if (notifications != null)
                await notifications.NotifyStatusChangedAsync(
                    riskId, risk.Code, oldStatus, RiskStatus.RiskAccepted, risk.OwnerId);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Kalıntı risk kabul bildirimi gönderilemedi: {Code}", risk.Code);
        }

        return (true, null);
    }

    public async Task<bool> UpdateMetadataAsync(int id, int? organizationId, int? departmentId, string? riskStrategy, int? userId = null)
    {
        var risk = await db.Risks.FindAsync(id);
        if (risk == null) return false;
        var orgName     = organizationId.HasValue  ? (await db.Organizations.FindAsync(organizationId.Value))?.Name  : null;
        var deptName    = departmentId.HasValue    ? (await db.Departments.FindAsync(departmentId.Value))?.Name      : null;
        var oldOrg      = risk.OrganizationId.HasValue ? (await db.Organizations.FindAsync(risk.OrganizationId.Value))?.Name : null;
        var oldStrategy = risk.RiskStrategy; // Güncellenmeden önce eski değeri yakala

        risk.OrganizationId = organizationId;
        risk.DepartmentId   = departmentId;
        if (riskStrategy != null) risk.RiskStrategy = string.IsNullOrEmpty(riskStrategy) ? null : riskStrategy;
        // Status geçişi yalnızca bu çağrıda strateji aktif olarak atanıyorsa tetiklenmeli
        if (!string.IsNullOrEmpty(riskStrategy) && risk.Status == "approved")
            risk.Status = "strategy_set";

        Log(id, userId, "Sorumluluk & Strateji Güncellendi", "Organizasyon", oldOrg, orgName);
        if (deptName != null)
            Log(id, userId, "Sorumluluk & Strateji Güncellendi", "Departman", null, deptName);
        if (!string.IsNullOrEmpty(riskStrategy))
            Log(id, userId, "Sorumluluk & Strateji Güncellendi", "Strateji", oldStrategy, riskStrategy);
        await db.SaveChangesAsync();
        return true;
    }

    [Obsolete("Blazor Server'da deadlock riski. UpdateMetadataAsync kullanın.", error: false)]
    public bool UpdateMetadata(int id, int? organizationId, int? departmentId, string? riskStrategy, int? userId = null)
        => UpdateMetadataAsync(id, organizationId, departmentId, riskStrategy, userId).GetAwaiter().GetResult();

    public async Task<bool> UpdateRiskFieldsAsync(int id, string sourceType, string? source, string? hazard,
        string? possibleImpact, string? affectedPersons, string? relevantLegislation,
        DateTime? lastReviewedAt, string? lastReviewerName, string? lastReviewerTitle,
        string? currentStatus = null, int? userId = null, string? category = null)
    {
        var risk = await db.Risks.FindAsync(id);
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

        await db.SaveChangesAsync();
        return true;
    }

    [Obsolete("Blazor Server'da deadlock riski. UpdateRiskFieldsAsync kullanın.", error: false)]
    public bool UpdateRiskFields(int id, string sourceType, string? source, string? hazard,
        string? possibleImpact, string? affectedPersons, string? relevantLegislation,
        DateTime? lastReviewedAt, string? lastReviewerName, string? lastReviewerTitle,
        string? currentStatus = null, int? userId = null, string? category = null)
        => UpdateRiskFieldsAsync(id, sourceType, source, hazard, possibleImpact, affectedPersons,
            relevantLegislation, lastReviewedAt, lastReviewerName, lastReviewerTitle,
            currentStatus, userId, category).GetAwaiter().GetResult();

    public async Task<bool> AssignOwnerAsync(int id, int ownerId, User currentUser)
    {
        if (!authSvc.HasPermission(currentUser, "risk.manage")) return false;
        var risk = await db.Risks.FindAsync(id); if (risk == null) return false;
        Log(id, currentUser.Id, "Risk Sahibi Atandı", "OwnerId",
            risk.OwnerId?.ToString(), ownerId.ToString());
        risk.OwnerId = ownerId;
        await db.SaveChangesAsync();
        try
        {
            if (notifications != null)
                await notifications.NotifyOwnerAssignedAsync(id, risk.Code, risk.Title, ownerId);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Sahip atama bildirimi gönderilemedi: {Code}", risk.Code);
        }
        return true;
    }

    [Obsolete("Blazor Server'da deadlock riski. AssignOwnerAsync kullanın.", error: false)]
    public bool AssignOwner(int id, int ownerId, User currentUser)
        => AssignOwnerAsync(id, ownerId, currentUser).GetAwaiter().GetResult();

    // ── Değerlendirme ────────────────────────────────────────────────────────
    public async Task<Evaluation> AddEvaluationAsync(int riskId, string evalType,
        double probability, double exposure, double consequence, string? notes, int evaluatedById)
    {
        var existing = await db.Evaluations.Where(e => e.RiskId == riskId && e.EvalType == evalType).ToListAsync();
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

        var risk = await db.Risks.FindAsync(riskId);
        if (risk != null && evalType == EvalType.Initial)
        {
            if (risk.Status == RiskStatus.Proposed)
            {
                // Henüz incelemeye alınmamış — skordan bağımsız olarak önce under_review'a al.
                risk.Status = RiskStatus.UnderReview;
            }
            else if (risk.Status == RiskStatus.UnderReview)
            {
                // İnceleme aşamasında: düşük riskler (skor < eşik) komite onayı atlanarak onaylanır.
                // Yüksek riskler manuel olarak awaiting_approval'a taşınır.
                var threshold = config?.GetAutoApproveScoreThreshold() ?? 70;
                if (score < threshold)
                    risk.Status = RiskStatus.Approved;
            }
        }

        if (risk != null && evalType == EvalType.Residual && risk.Status == RiskStatus.Controlled)
            risk.Status = RiskStatus.ResidualEvaluated;

        Log(riskId, evaluatedById,
            evalType == EvalType.Initial ? "İlk Değerlendirme Yapıldı" : "Kalan Risk Değerlendirmesi Yapıldı",
            "Skor", existing.FirstOrDefault()?.Score.ToString(), score.ToString());
        await db.SaveChangesAsync();
        return eval;
    }

    [Obsolete("Blazor Server'da deadlock riski. AddEvaluationAsync kullanın.", error: false)]
    public Evaluation AddEvaluation(int riskId, string evalType,
        double probability, double exposure, double consequence, string? notes, int evaluatedById)
        => AddEvaluationAsync(riskId, evalType, probability, exposure, consequence, notes, evaluatedById)
            .GetAwaiter().GetResult();

    // ── Kontroller ───────────────────────────────────────────────────────────
    public async Task<Control> AddControlAsync(int riskId, string description, string controlType,
        string? effectiveness, string? frequency, int enteredById, int? ownerDeptId = null)
    {
        var ctrl = new Control
        {
            RiskId = riskId, Description = description, ControlType = controlType,
            Effectiveness = effectiveness, Frequency = frequency,
            EnteredById = enteredById, OwnerDeptId = ownerDeptId > 0 ? ownerDeptId : null,
        };
        db.Controls.Add(ctrl);

        var risk = await db.Risks.FindAsync(riskId);
        if (risk != null && risk.Status == RiskStatus.StrategySet) risk.Status = RiskStatus.Controlled;

        Log(riskId, enteredById, "Kontrol Eklendi", "Açıklama", null, description);
        await db.SaveChangesAsync();
        return ctrl;
    }

    [Obsolete("Blazor Server'da deadlock riski. AddControlAsync kullanın.", error: false)]
    public Control AddControl(int riskId, string description, string controlType,
        string? effectiveness, string? frequency, int enteredById, int? ownerDeptId = null)
        => AddControlAsync(riskId, description, controlType, effectiveness, frequency, enteredById, ownerDeptId)
            .GetAwaiter().GetResult();

    public async Task<bool> EditControlAsync(int riskId, int controlId, string description,
        string controlType, string? effectiveness, string? frequency, int? ownerDeptId, int? userId = null)
    {
        var ctrl = await db.Controls.FirstOrDefaultAsync(c => c.Id == controlId && c.RiskId == riskId);
        if (ctrl == null) return false;

        var changes = new List<string>();
        if (ctrl.Description != description) changes.Add($"Açıklama: \"{ctrl.Description}\" → \"{description}\"");
        if (ctrl.ControlType != controlType) changes.Add($"Tür: {ctrl.ControlType} → {controlType}");

        ctrl.Description = description; ctrl.ControlType = controlType;
        ctrl.Effectiveness = effectiveness; ctrl.Frequency = frequency;
        ctrl.OwnerDeptId = ownerDeptId > 0 ? ownerDeptId : null;

        Log(riskId, userId, "Kontrol Düzenlendi", "Kontrol", null,
            changes.Any() ? string.Join("; ", changes) : "Güncellendi");
        await db.SaveChangesAsync();
        return true;
    }

    [Obsolete("Blazor Server'da deadlock riski. EditControlAsync kullanın.", error: false)]
    public bool EditControl(int riskId, int controlId, string description,
        string controlType, string? effectiveness, string? frequency, int? ownerDeptId, int? userId = null)
        => EditControlAsync(riskId, controlId, description, controlType, effectiveness, frequency, ownerDeptId, userId)
            .GetAwaiter().GetResult();

    public async Task<bool> DeleteControlAsync(int riskId, int controlId, int? userId = null)
    {
        var ctrl = await db.Controls.FirstOrDefaultAsync(c => c.Id == controlId && c.RiskId == riskId);
        if (ctrl == null) return false;
        Log(riskId, userId, "Kontrol Silindi", "Açıklama", ctrl.Description, null);
        db.Controls.Remove(ctrl);
        await db.SaveChangesAsync();
        return true;
    }

    [Obsolete("Blazor Server'da deadlock riski. DeleteControlAsync kullanın.", error: false)]
    public bool DeleteControl(int riskId, int controlId, int? userId = null)
        => DeleteControlAsync(riskId, controlId, userId).GetAwaiter().GetResult();

    // ── Aksiyon Planları ─────────────────────────────────────────────────────
    public async Task<ActionPlan> AddActionAsync(int riskId, string description, string responsible,
        DateOnly? dueDate, int createdById, int? ownerDeptId = null)
    {
        var action = new ActionPlan
        {
            RiskId = riskId, Description = description, Responsible = responsible,
            DueDate = dueDate, CreatedById = createdById,
            OwnerDeptId = ownerDeptId > 0 ? ownerDeptId : null,
        };
        db.ActionPlans.Add(action);

        var risk = await db.Risks.FindAsync(riskId);
        if (risk != null && risk.Status == RiskStatus.ResidualEvaluated) risk.Status = RiskStatus.ActionPlanned;

        Log(riskId, createdById, "Aksiyon Eklendi", "Açıklama", null, description);
        await db.SaveChangesAsync();
        return action;
    }

    [Obsolete("Blazor Server'da deadlock riski. AddActionAsync kullanın.", error: false)]
    public ActionPlan AddAction(int riskId, string description, string responsible,
        DateOnly? dueDate, int createdById, int? ownerDeptId = null)
        => AddActionAsync(riskId, description, responsible, dueDate, createdById, ownerDeptId)
            .GetAwaiter().GetResult();

    public async Task<bool> EditActionAsync(int riskId, int actionId, string description,
        int? ownerDeptId, DateOnly? dueDate, int? userId = null)
    {
        var action = await db.ActionPlans.FirstOrDefaultAsync(a => a.Id == actionId && a.RiskId == riskId);
        if (action == null) return false;

        var changes = new List<string>();
        if (action.Description != description) changes.Add($"Açıklama: \"{action.Description}\" → \"{description}\"");
        if (action.DueDate != dueDate) changes.Add($"Hedef Tarih: {action.DueDate} → {dueDate}");

        action.Description = description;
        action.OwnerDeptId = ownerDeptId > 0 ? ownerDeptId : null;
        action.DueDate = dueDate;

        Log(riskId, userId, "Aksiyon Düzenlendi", "Aksiyon", null,
            changes.Any() ? string.Join("; ", changes) : "Güncellendi");
        await db.SaveChangesAsync();
        return true;
    }

    [Obsolete("Blazor Server'da deadlock riski. EditActionAsync kullanın.", error: false)]
    public bool EditAction(int riskId, int actionId, string description,
        int? ownerDeptId, DateOnly? dueDate, int? userId = null)
        => EditActionAsync(riskId, actionId, description, ownerDeptId, dueDate, userId)
            .GetAwaiter().GetResult();

    public async Task<bool> UpdateActionStatusAsync(int riskId, int actionId, string newStatus, int? userId = null)
    {
        var action = await db.ActionPlans.FirstOrDefaultAsync(a => a.Id == actionId && a.RiskId == riskId);
        if (action == null) return false;

        var oldStatus = action.Status;
        action.Status = newStatus;
        if (newStatus == ActionStatus.Completed) action.CompletedAt = DateTime.UtcNow;

        Log(riskId, userId, "Aksiyon Durumu Güncellendi", "Durum",
            ActionStatusLabel(oldStatus), ActionStatusLabel(newStatus));

        // Tüm aksiyonlar tamamlandıysa kalan riski yeniden değerlendirmeye gerek var
        await db.SaveChangesAsync();

        var allDone = !await db.ActionPlans.AnyAsync(a => a.RiskId == riskId
            && a.Status != ActionStatus.Completed && a.Status != ActionStatus.Cancelled);
        if (allDone && newStatus == ActionStatus.Completed)
        {
            var risk = await db.Risks.FindAsync(riskId);
            if (risk?.Status == RiskStatus.ActionPlanned)
            {
                risk.Status = RiskStatus.ResidualEvaluated;
                Log(riskId, userId, "Tüm Aksiyonlar Tamamlandı — Kalan Risk Yeniden Değerlendirilmeli",
                    "Durum", "Aksiyon Planlandı", "Kalan Risk");
                await db.SaveChangesAsync();
            }
        }
        return true;
    }

    [Obsolete("Blazor Server'da deadlock riski. UpdateActionStatusAsync kullanın.", error: false)]
    public bool UpdateActionStatus(int riskId, int actionId, string newStatus, int? userId = null)
        => UpdateActionStatusAsync(riskId, actionId, newStatus, userId).GetAwaiter().GetResult();

    public async Task<bool> DeleteActionAsync(int riskId, int actionId, int? userId = null)
    {
        var action = await db.ActionPlans.FirstOrDefaultAsync(a => a.Id == actionId && a.RiskId == riskId);
        if (action == null) return false;
        Log(riskId, userId, "Aksiyon Silindi", "Açıklama", action.Description, null);
        db.ActionPlans.Remove(action);
        await db.SaveChangesAsync();
        return true;
    }

    [Obsolete("Blazor Server'da deadlock riski. DeleteActionAsync kullanın.", error: false)]
    public bool DeleteAction(int riskId, int actionId, int? userId = null)
        => DeleteActionAsync(riskId, actionId, userId).GetAwaiter().GetResult();

    // ── Gözden Geçirmeler ────────────────────────────────────────────────────
    public async Task<RiskReview> AddReviewAsync(int riskId, DateTime meetingDate, string? decision,
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
        await db.SaveChangesAsync();
        return review;
    }

    [Obsolete("Blazor Server'da deadlock riski. AddReviewAsync kullanın.", error: false)]
    public RiskReview AddReview(int riskId, DateTime meetingDate, string? decision,
        string? notes, int createdById)
        => AddReviewAsync(riskId, meetingDate, decision, notes, createdById).GetAwaiter().GetResult();

    public async Task DeleteReviewAsync(int reviewId, int? userId = null)
    {
        var r = await db.RiskReviews.FindAsync(reviewId);
        if (r == null) return;
        Log(r.RiskId, userId, "Gözden Geçirme Silindi",
            "Toplantı Tarihi", r.MeetingDate.ToString("dd.MM.yyyy"), null);
        db.RiskReviews.Remove(r);
        await db.SaveChangesAsync();
    }

    [Obsolete("Blazor Server'da deadlock riski. DeleteReviewAsync kullanın.", error: false)]
    public void DeleteReview(int reviewId, int? userId = null)
        => DeleteReviewAsync(reviewId, userId).GetAwaiter().GetResult();

    // ── Önceki / Sonraki navigasyon ──────────────────────────────────────────

    // Include'suz hafif sorgu — sadece navigasyon ID listesi için kullanılır
    private IQueryable<Risk> BuildUserQueryIds(int userId, string role)
    {
        var q = db.Risks.AsQueryable();
        if (!IsRiskManager(userId, role))
        {
            var userDeptIds = GetUserDepartmentIds(userId);
            q = q.Where(r =>
                r.ProposedById == userId || r.OwnerId == userId ||
                (r.DepartmentId != null && userDeptIds.Contains(r.DepartmentId.Value)));
        }
        return q;
    }

    public (int? PrevId, int? NextId) GetAdjacentIds(int riskId, int userId, string role)
    {
        var ids = BuildUserQueryIds(userId, role)
            .OrderByDescending(r => r.ProposedAt)
            .Select(r => r.Id)
            .ToList();

        var idx = ids.IndexOf(riskId);
        if (idx < 0) return (null, null);

        return (
            idx > 0             ? ids[idx - 1] : (int?)null,
            idx < ids.Count - 1 ? ids[idx + 1] : (int?)null
        );
    }

    // ── Dashboard / Radar ────────────────────────────────────────────────────
    public List<CategoryRadarData> GetRadarData()
    {
        // Sadece gerekli alanlar — 10+ Include zinciri yok
        var riskCats = db.Risks
            .Where(r => r.Category != null)
            .Select(r => new { r.Id, r.Category })
            .ToList();

        if (riskCats.Count == 0) return [];

        var riskIds = riskCats.Select(r => r.Id).ToHashSet();
        var evals = db.Evaluations
            .Where(e => riskIds.Contains(e.RiskId))
            .Select(e => new { e.RiskId, e.EvalType, e.Score })
            .ToList();

        var catMap = riskCats.ToDictionary(r => r.Id, r => r.Category!);

        return riskCats
            .GroupBy(r => r.Category!)
            .Select(g =>
            {
                var ids    = g.Select(r => r.Id).ToHashSet();
                var inits  = evals.Where(e => ids.Contains(e.RiskId) && e.EvalType == "initial").ToList();
                var resids = evals.Where(e => ids.Contains(e.RiskId) && e.EvalType == "residual").ToList();
                return new CategoryRadarData
                {
                    Category    = g.Key,
                    Count       = g.Count(),
                    AvgInitial  = inits.Count  > 0 ? Math.Round(inits.Average(e => e.Score),  1) : 0,
                    MaxInitial  = inits.Count  > 0 ? inits.Max(e => e.Score)  : 0,
                    AvgResidual = resids.Count > 0 ? Math.Round(resids.Average(e => e.Score), 1) : 0,
                    MaxResidual = resids.Count > 0 ? resids.Max(e => e.Score) : 0,
                    AvgReduction = inits.Count > 0 && resids.Count > 0
                        ? (int?)Math.Round((1 - resids.Average(e => e.Score) / inits.Average(e => e.Score)) * 100)
                        : null
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

        if (!IsRiskManager(userId, role))
        {
            var deptIds = GetUserDepartmentIds(userId);
            q = q.Where(a => a.Risk != null && (
                a.Risk.ProposedById == userId ||
                a.Risk.OwnerId == userId ||
                (a.Risk.DepartmentId != null && deptIds.Contains(a.Risk.DepartmentId.Value))));
        }

        return [.. q.OrderBy(a => a.DueDate)];
    }

    public List<ActionPlan> GetOverdueActions(User user)
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

        if (!authSvc.HasPermission(user, "risk.manage"))
        {
            var deptIds = user.AllDepartmentIds.ToHashSet();
            var userId  = user.Id;
            q = q.Where(a => a.Risk != null && (
                a.Risk.ProposedById == userId ||
                a.Risk.OwnerId == userId ||
                (a.Risk.DepartmentId != null && deptIds.Contains(a.Risk.DepartmentId.Value))));
        }

        return [.. q.OrderBy(a => a.DueDate)];
    }

    public DashboardStats GetDashboardStats()
    {
        var counts = db.Risks
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionary(x => x.Status, x => x.Count);

        int Get(string key) => counts.GetValueOrDefault(key, 0);

        // Dashboard grupları kasıtlı olarak birleştirilmiş — bkz. tasarım notları:
        // Approved  = "approved" + "strategy_set"          (strateji atanmış ama henüz kontrolsüz)
        // ActionPlanned = "action_planned" + "risk_accepted" (aksiyon döngüsünün son halkası)
        // "controlled", "residual_evaluated", "closed" bilinçli olarak ayrı bucket'a konulmadı;
        // Total bu durumları kapsadığından gösterge kartları toplamına ≠ Total'dır.
        return new DashboardStats
        {
            Total            = counts.Values.Sum(),
            Proposed         = Get("proposed"),
            UnderReview      = Get("under_review"),
            AwaitingApproval = Get("awaiting_approval"),
            Approved         = Get("approved") + Get("strategy_set"),
            Rejected         = Get("rejected"),
            Controlled       = Get("controlled"),
            ActionPlanned    = Get("action_planned") + Get("risk_accepted"),
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
    public int Total, Proposed, UnderReview, AwaitingApproval, Approved, Rejected, Controlled, ActionPlanned;
}
