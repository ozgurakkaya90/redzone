using RiskManagement.Models;

namespace RiskManagement.Data;

public static class SeedData
{
    public static async Task RunAsync(AppDbContext db)
    {
        if (db.Users.Any()) return; // Zaten seed edilmiş

        // ─── Kullanıcılar ─────────────────────────────────────────────────────
        // Tüm şifreler rastgele üretilir ve konsola yazdırılır.
        // SEED_ADMIN_PASSWORD env var ile admin şifresi önceden belirlenebilir.
        var adminPassword = Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD")
                            ?? GenerateRandomPassword();

        var demoPasswords = new Dictionary<string, string>
        {
            ["komite1"]    = GenerateRandomPassword(),
            ["riskowner1"] = GenerateRandomPassword(),
            ["riskymgr1"]  = GenerateRandomPassword(),
            ["denetci1"]   = GenerateRandomPassword(),
            ["denetimmgr"] = GenerateRandomPassword(),
        };

        Console.WriteLine("==========================================================");
        Console.WriteLine("  İLK KURULUM — Seed kullanıcı şifreleri (bir kez gösterilir)");
        Console.WriteLine("==========================================================");
        Console.WriteLine($"  admin        : {adminPassword}");
        foreach (var (user, pass) in demoPasswords)
            Console.WriteLine($"  {user,-12} : {pass}");
        Console.WriteLine("  Bu şifreler veritabanında saklanmaz; yalnızca burada gösterilir.");
        Console.WriteLine("  Lütfen giriş yaptıktan sonra şifrenizi değiştirin.");
        Console.WriteLine("==========================================================");

        var admin = new User
        {
            Username = "admin", FullName = "Sistem Yöneticisi",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
        };
        var committee = new User
        {
            Username = "komite1", FullName = "Ayşe Kaya",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(demoPasswords["komite1"]),
        };
        var owner = new User
        {
            Username = "riskowner1", FullName = "Mehmet Yılmaz",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(demoPasswords["riskowner1"]),
        };
        var riskMgr = new User
        {
            Username = "riskymgr1", FullName = "Fatma Arslan",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(demoPasswords["riskymgr1"]),
        };
        var auditor = new User
        {
            Username = "denetci1", FullName = "Zeynep Demir",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(demoPasswords["denetci1"]),
        };
        var auditMgr = new User
        {
            Username = "denetimmgr", FullName = "Can Şahin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(demoPasswords["denetimmgr"]),
        };

        db.Users.AddRange(admin, committee, owner, riskMgr, auditor, auditMgr);

        // ─── Departmanlar ─────────────────────────────────────────────────────
        var departments = new[]
        {
            new Department { Name = "Yönetim" },
            new Department { Name = "Risk Komitesi" },
            new Department { Name = "Operasyon" },
            new Department { Name = "İç Denetim" },
            new Department { Name = "Bilgi Teknolojileri" },
            new Department { Name = "İnsan Kaynakları" },
            new Department { Name = "Finans" },
            new Department { Name = "Hukuk & Uyum" },
            new Department { Name = "Satın Alma" },
            new Department { Name = "Satış & Pazarlama" },
        };
        db.Departments.AddRange(departments);

        await db.SaveChangesAsync();

        // ─── Varsayılan İzinler ───────────────────────────────────────────────
        var roles = new[] { "committee", "risk_manager", "risk_owner", "user", "auditor", "audit_manager", "finding_owner", "ethics_board" };
        foreach (var role in roles)
        {
            if (Services.AuthService.DefaultPermissions.TryGetValue(role, out var perms))
            {
                foreach (var perm in perms)
                    db.RolePermissions.Add(new RolePermission { Role = role, Permission = perm });
            }
        }

        // ─── Seed kullanıcılarına UserRole kayıtları ─────────────────────────
        db.UserRoles.AddRange(
            new UserRole { User = admin,     RoleName = "admin" },
            new UserRole { User = committee, RoleName = "committee" },
            new UserRole { User = owner,     RoleName = "risk_owner" },
            new UserRole { User = riskMgr,   RoleName = "risk_manager" },
            new UserRole { User = auditor,   RoleName = "auditor" },
            new UserRole { User = auditMgr,  RoleName = "audit_manager" }
        );

        // ─── Örnek Riskler ────────────────────────────────────────────────────
        var risks = new[]
        {
            new Risk { Code="R-2026-001", Title="Veri İhlali Riski", Category="Bilgi Teknolojileri",
                Status="action_planned", ProposedById=owner.Id, OwnerId=owner.Id,
                Description="Müşteri verilerinin yetkisiz erişime maruz kalma riski." },
            new Risk { Code="R-2026-002", Title="Tedarik Zinciri Kesintisi", Category="Operasyonel",
                Status="controlled", ProposedById=owner.Id, OwnerId=owner.Id,
                RiskStrategy="Riski Azaltma",
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

    private static string GenerateRandomPassword()
    {
        // 24 byte -> 32 char base64 string, yeterince güçlü demo parola
        var bytes = new byte[24];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
