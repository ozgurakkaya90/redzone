using RiskManagement.Models;

namespace RiskManagement.Data;

public static class SeedData
{
    public static async Task RunAsync(AppDbContext db)
    {
        if (db.Users.Any()) return; // Zaten seed edilmiş

        // ─── Kullanıcılar ─────────────────────────────────────────────────────
        var admin = new User
        {
            Username = "admin", FullName = "Sistem Yöneticisi",
            Department = "Yönetim", Role = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
        };
        var committee = new User
        {
            Username = "komite1", FullName = "Ayşe Kaya",
            Department = "Risk Komitesi", Role = "committee",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("komite123"),
        };
        var owner = new User
        {
            Username = "riskowner1", FullName = "Mehmet Yılmaz",
            Department = "Operasyon", Role = "risk_owner",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"),
        };
        var auditor = new User
        {
            Username = "denetci1", FullName = "Zeynep Demir",
            Department = "İç Denetim", Role = "auditor",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("denetci123"),
        };
        var auditMgr = new User
        {
            Username = "denetimmgr", FullName = "Can Şahin",
            Department = "İç Denetim", Role = "audit_manager",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123"),
        };

        db.Users.AddRange(admin, committee, owner, auditor, auditMgr);
        await db.SaveChangesAsync();

        // ─── Varsayılan İzinler ───────────────────────────────────────────────
        var roles = new[] { "committee", "risk_owner", "user", "auditor", "audit_manager", "finding_owner", "ethics_board" };
        foreach (var role in roles)
        {
            if (Services.AuthService.DefaultPermissions.TryGetValue(role, out var perms))
            {
                foreach (var perm in perms)
                    db.RolePermissions.Add(new RolePermission { Role = role, Permission = perm });
            }
        }

        // ─── Örnek Riskler ────────────────────────────────────────────────────
        var risks = new[]
        {
            new Risk { Code="R-2026-001", Title="Veri İhlali Riski", Category="Bilgi Teknolojileri",
                Status="action_planned", ProposedById=owner.Id, OwnerId=owner.Id,
                Description="Müşteri verilerinin yetkisiz erişime maruz kalma riski." },
            new Risk { Code="R-2026-002", Title="Tedarik Zinciri Kesintisi", Category="Operasyonel",
                Status="controlled", ProposedById=owner.Id, OwnerId=owner.Id,
                ResponsibleUnit="İdari Koordinatörlük", RiskStrategy="Riski Azaltma",
                Description="Kritik tedarikçi kesintilerinden kaynaklanan operasyonel aksaklık." },
            new Risk { Code="R-2026-003", Title="Döviz Kuru Riski", Category="Finansal",
                Status="approved", ProposedById=admin.Id,
                Description="Döviz kurundaki dalgalanmaların finansal tablolara etkisi." },
            new Risk { Code="R-2026-004", Title="Regülasyon Uyum Riski", Category="Uyum",
                Status="proposed", ProposedById=owner.Id,
                Description="Yeni mevzuat gerekliliklerine uyum sağlanmaması." },
        };
        db.Risks.AddRange(risks);
        await db.SaveChangesAsync();

        // ─── Değerlendirmeler ─────────────────────────────────────────────────
        db.Evaluations.Add(new Evaluation
        {
            RiskId = risks[0].Id, EvalType = "initial",
            Probability = 6, Exposure = 3, Consequence = 40,
            Score = 720, RiskLevel = "Çok Yüksek Risk",
            EvaluatedById = committee.Id,
        });
        db.Evaluations.Add(new Evaluation
        {
            RiskId = risks[0].Id, EvalType = "residual",
            Probability = 3, Exposure = 3, Consequence = 15,
            Score = 135, RiskLevel = "Yüksek Risk",
            EvaluatedById = committee.Id,
        });
        db.Evaluations.Add(new Evaluation
        {
            RiskId = risks[1].Id, EvalType = "initial",
            Probability = 3, Exposure = 6, Consequence = 15,
            Score = 270, RiskLevel = "Yüksek Risk",
            EvaluatedById = committee.Id,
        });

        // ─── Kontroller ───────────────────────────────────────────────────────
        db.Controls.Add(new Control
        {
            RiskId = risks[0].Id, Description = "Çok faktörlü kimlik doğrulama zorunluluğu.",
            ControlType = "Önleyici", Effectiveness = "Tatmin Edici", Frequency = "Sürekli",
            EnteredById = owner.Id,
        });
        db.Controls.Add(new Control
        {
            RiskId = risks[1].Id, Description = "Alternatif tedarikçi listesi hazırlanması.",
            ControlType = "Düzeltici", Effectiveness = "Gelişmekte", Frequency = "3 Aylık",
            EnteredById = owner.Id,
        });

        // ─── İç Denetimler ────────────────────────────────────────────────────
        var audit = new InternalAudit
        {
            Code = "ID-2026-001", Title = "2026 Yılı Mali İşlemler Denetimi",
            AuditType = "ordinary", Period = "2026 Q1",
            Status = "in_progress", LeadAuditorId = auditor.Id,
        };
        db.InternalAudits.Add(audit);
        await db.SaveChangesAsync();

        // ─── Bulgular ─────────────────────────────────────────────────────────
        var findings = new[]
        {
            new AuditFinding
            {
                Code="B-2026-001", Title="Onaysız ödeme talimatları",
                Category="Mali", Severity="Yüksek", AuditPeriod="2026 Q1",
                AuditorId=auditor.Id, InternalAuditId=audit.Id,
                DueDate=DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            },
            new AuditFinding
            {
                Code="B-2026-002", Title="Stok sayım tutarsızlıkları",
                Category="Operasyonel", Severity="Orta", AuditPeriod="2026 Q1",
                AuditorId=auditor.Id, InternalAuditId=audit.Id,
                DueDate=DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45)),
            },
        };
        db.AuditFindings.AddRange(findings);

        await db.SaveChangesAsync();
    }
}
