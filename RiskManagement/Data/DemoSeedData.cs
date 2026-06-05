using BCrypt.Net;
using RiskManagement.Models;
using RiskManagement.Services;

namespace RiskManagement.Data;

/// <summary>
/// Demo ortamı için kapsamlı test verisi. Risk Kütüphanesi dışındaki tüm veriyi temizler ve yeniden oluşturur.
/// AppSettings:DemoReset=true + AppConfigs.demo_seed_version != "3" koşulunda çalışır.
/// </summary>
public static class DemoSeedData
{
    public const string SeedVersion = "4";

    public static async Task ResetAndSeedAsync(AppDbContext db)
    {
        // ─── Temizlik — FK sırasına göre ─────────────────────────────────────
        db.ConfigChangeLogs.RemoveRange(db.ConfigChangeLogs);
        db.UserExternalAuditBodies.RemoveRange(db.UserExternalAuditBodies);
        db.RiskFindingLinks.RemoveRange(db.RiskFindingLinks);
        db.ClosureRequests.RemoveRange(db.ClosureRequests);
        db.AuditFindingActions.RemoveRange(db.AuditFindingActions);
        db.FindingActivityLogs.RemoveRange(db.FindingActivityLogs);
        db.FindingAttachments.RemoveRange(db.FindingAttachments);
        db.AuditFindings.RemoveRange(db.AuditFindings);
        db.InternalAudits.RemoveRange(db.InternalAudits);
        db.AuditPlanItems.RemoveRange(db.AuditPlanItems);
        db.AuditPlans.RemoveRange(db.AuditPlans);
        db.ExternalAudits.RemoveRange(db.ExternalAudits);
        db.ExternalAuditBodies.RemoveRange(db.ExternalAuditBodies);
        db.EthicsAttachments.RemoveRange(db.EthicsAttachments);
        db.EthicsReports.RemoveRange(db.EthicsReports);
        db.ActionPlans.RemoveRange(db.ActionPlans);
        db.Controls.RemoveRange(db.Controls);
        db.Evaluations.RemoveRange(db.Evaluations);
        db.RiskReviews.RemoveRange(db.RiskReviews);
        db.RiskAuditLogs.RemoveRange(db.RiskAuditLogs);
        db.Risks.RemoveRange(db.Risks);
        db.UserRoles.RemoveRange(db.UserRoles);
        db.UserDepartments.RemoveRange(db.UserDepartments);
        db.UserOrganizations.RemoveRange(db.UserOrganizations);
        db.UserCompanies.RemoveRange(db.UserCompanies);
        db.PasswordResetTokens.RemoveRange(db.PasswordResetTokens);
        db.Users.RemoveRange(db.Users);
        db.Departments.RemoveRange(db.Departments);
        db.Organizations.RemoveRange(db.Organizations);
        db.Companies.RemoveRange(db.Companies);
        db.RolePermissions.RemoveRange(db.RolePermissions);
        db.Counters.RemoveRange(db.Counters);
        await db.SaveChangesAsync();

        // ─── Şirketler ────────────────────────────────────────────────────────
        var holding  = new Company { Name = "RED Holding A.Ş.",       Description = "Grup ana şirketi" };
        var tekAŞ    = new Company { Name = "RED Teknoloji A.Ş.",      Description = "Yazılım ve BT hizmetleri" };
        var finAŞ    = new Company { Name = "RED Finans A.Ş.",         Description = "Bankacılık ve finans hizmetleri" };
        db.Companies.AddRange(holding, tekAŞ, finAŞ);
        await db.SaveChangesAsync();

        // ─── Organizasyonlar ──────────────────────────────────────────────────
        var orgGMD  = new Organization { Name = "Genel Müdürlük",           CompanyId = holding.Id };
        var orgYaz  = new Organization { Name = "Yazılım Ürünleri",          CompanyId = tekAŞ.Id };
        var orgInf  = new Organization { Name = "Altyapı & Güvenlik",        CompanyId = tekAŞ.Id };
        var orgKB   = new Organization { Name = "Kurumsal Bankacılık",        CompanyId = finAŞ.Id };
        var orgHaz  = new Organization { Name = "Hazine & Yatırım",           CompanyId = finAŞ.Id };
        var orgRisk = new Organization { Name = "Risk & Uyum",                CompanyId = finAŞ.Id };
        db.Organizations.AddRange(orgGMD, orgYaz, orgInf, orgKB, orgHaz, orgRisk);
        await db.SaveChangesAsync();

        // ─── Departmanlar ─────────────────────────────────────────────────────
        var dYK    = new Department { Name = "Yönetim Kurulu",       OrganizationId = orgGMD.Id };
        var dRisk  = new Department { Name = "Risk Yönetimi",         OrganizationId = orgRisk.Id };
        var dDen   = new Department { Name = "İç Denetim",            OrganizationId = orgGMD.Id };
        var dBT    = new Department { Name = "Bilgi Teknolojileri",   OrganizationId = orgInf.Id };
        var dSW    = new Department { Name = "Yazılım Geliştirme",    OrganizationId = orgYaz.Id };
        var dFin   = new Department { Name = "Finans & Muhasebe",     OrganizationId = orgKB.Id };
        var dIK    = new Department { Name = "İnsan Kaynakları",      OrganizationId = orgGMD.Id };
        var dHukuk = new Department { Name = "Hukuk & Uyum",          OrganizationId = orgRisk.Id };
        var dOps   = new Department { Name = "Operasyon & Lojistik",  OrganizationId = orgKB.Id };
        var dSatın = new Department { Name = "Satın Alma",             OrganizationId = orgGMD.Id };
        var dSatiş = new Department { Name = "Satış & Pazarlama",     OrganizationId = orgYaz.Id };
        var dSiber = new Department { Name = "Siber Güvenlik",         OrganizationId = orgInf.Id };
        db.Departments.AddRange(dYK, dRisk, dDen, dBT, dSW, dFin, dIK, dHukuk, dOps, dSatın, dSatiş, dSiber);
        await db.SaveChangesAsync();

        // ─── Varsayılan İzinler ───────────────────────────────────────────────
        foreach (var (role, perms) in AuthService.DefaultPermissions)
            foreach (var perm in perms)
                db.RolePermissions.Add(new RolePermission { Role = role, Permission = perm });
        await db.SaveChangesAsync();

        // ─── Kullanıcılar — Ortalama şirket kadrosu ──────────────────────────
        // Parola: Admin123! (admin), Demo123! (diğerleri)
        var pw = (string s) => BCrypt.Net.BCrypt.HashPassword(s);

        // ── Sistem & Demo ──────────────────────────────────────────────────────
        var uAdmin  = new User { Username="admin",    FullName="Sistem Yöneticisi",          Role="admin",         AuthType="local", IsActive=true, PasswordHash=pw("Admin123!"), DepartmentId=dYK.Id,    OrganizationId=orgGMD.Id,  CompanyId=holding.Id };
        var uDemo   = new User { Username="demo",     FullName="Demo Yönetici",               Role="admin",         AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dYK.Id,    OrganizationId=orgGMD.Id,  CompanyId=holding.Id };

        // ── Risk Komitesi (3 kişi — CFO, COO, Risk Direktörü) ─────────────────
        var uKom1   = new User { Username="t.yilmaz", FullName="Taner Yılmaz",                Role="committee",     AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dYK.Id,    OrganizationId=orgGMD.Id,  CompanyId=holding.Id };
        var uKom2   = new User { Username="a.kaya",   FullName="Ayşe Kaya",                Role="committee",     AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dFin.Id,   OrganizationId=orgKB.Id,   CompanyId=finAŞ.Id };
        var uKom3   = new User { Username="o.celik",  FullName="Osman Çelik",                  Role="committee",     AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dRisk.Id,  OrganizationId=orgRisk.Id, CompanyId=finAŞ.Id };

        // ── Risk Yönetimi (2 kişi) ────────────────────────────────────────────
        var uRMgr1  = new User { Username="f.arslan", FullName="Fatma Arslan",                  Role="risk_manager",  AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dRisk.Id,  OrganizationId=orgRisk.Id, CompanyId=finAŞ.Id };
        var uRMgr2  = new User { Username="b.ozkan",  FullName="Burak Özkan",                   Role="risk_manager",  AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dRisk.Id,  OrganizationId=orgRisk.Id, CompanyId=finAŞ.Id };

        // ── Risk Sahipleri — Departman yöneticileri (4 kişi) ──────────────────
        var uROwn1  = new User { Username="m.yilmaz", FullName="Mehmet Yılmaz",           Role="risk_owner",    AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dOps.Id,   OrganizationId=orgKB.Id,   CompanyId=finAŞ.Id };
        var uROwn2  = new User { Username="a.celik",  FullName="Ali Çelik",               Role="risk_owner",    AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dBT.Id,    OrganizationId=orgInf.Id,  CompanyId=tekAŞ.Id };
        var uROwn3  = new User { Username="g.demir",  FullName="Gülsüm Demir",              Role="risk_owner",    AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dFin.Id,   OrganizationId=orgKB.Id,   CompanyId=finAŞ.Id };
        var uROwn4  = new User { Username="k.sahin",  FullName="Kemal Şahin",                Role="risk_owner",    AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dIK.Id,    OrganizationId=orgGMD.Id,  CompanyId=holding.Id };

        // ── İç Denetim (1 müdür + 2 denetçi) ────────────────────────────────
        var uDenMgr = new User { Username="c.sahin",  FullName="Can Şahin",                        Role="audit_manager", AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dDen.Id,   OrganizationId=orgGMD.Id,  CompanyId=holding.Id };
        var uDen1   = new User { Username="z.demir",  FullName="Zeynep Demir",                     Role="auditor",       AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dDen.Id,   OrganizationId=orgGMD.Id,  CompanyId=holding.Id };
        var uDen2   = new User { Username="y.polat",  FullName="Yunus Polat",                      Role="auditor",       AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dDen.Id,   OrganizationId=orgGMD.Id,  CompanyId=holding.Id };

        // ── Bulgu Sahipleri — Bulgularla görevlendirilen çalışanlar (3 kişi) ──
        var uFOwn1  = new User { Username="h.polat",  FullName="Hasan Polat",               Role="finding_owner", AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dFin.Id,   OrganizationId=orgKB.Id,   CompanyId=finAŞ.Id };
        var uFOwn2  = new User { Username="p.arslan", FullName="Pınar Arslan",            Role="finding_owner", AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dBT.Id,    OrganizationId=orgInf.Id,  CompanyId=tekAŞ.Id };
        var uFOwn3  = new User { Username="r.kurt",   FullName="Rıdvan Kurt",                      Role="finding_owner", AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dSatın.Id, OrganizationId=orgGMD.Id,  CompanyId=holding.Id };

        // ── Etik Kurul (2 kişi) ───────────────────────────────────────────────
        var uEth1   = new User { Username="s.yildiz", FullName="Selin Yıldız",                   Role="ethics_board",  AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dHukuk.Id, OrganizationId=orgRisk.Id, CompanyId=finAŞ.Id };
        var uEth2   = new User { Username="d.ozturk", FullName="Deniz Öztürk",               Role="ethics_board",  AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dIK.Id,    OrganizationId=orgGMD.Id,  CompanyId=holding.Id };

        // ── Standart Kullanıcılar — Risk öneri yapan çalışanlar (5 kişi) ─────
        var uUser1  = new User { Username="e.kurt",   FullName="Esra Kurt",                 Role="user",          AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dSatiş.Id, OrganizationId=orgYaz.Id,  CompanyId=tekAŞ.Id };
        var uUser2  = new User { Username="n.arslan", FullName="Neslihan Arslan",             Role="user",          AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dIK.Id,    OrganizationId=orgGMD.Id,  CompanyId=holding.Id };
        var uUser3  = new User { Username="i.yuce",   FullName="İbrahim Yüce",             Role="user",          AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dSW.Id,    OrganizationId=orgYaz.Id,  CompanyId=tekAŞ.Id };
        var uUser4  = new User { Username="l.koc",    FullName="Leyla Koç",                        Role="user",          AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dSatın.Id, OrganizationId=orgGMD.Id,  CompanyId=holding.Id };
        var uUser5  = new User { Username="m.ozdemir",FullName="Mustafa Özdemir",               Role="user",          AuthType="local", IsActive=true, PasswordHash=pw("Demo123!"),  DepartmentId=dSiber.Id, OrganizationId=orgInf.Id,  CompanyId=tekAŞ.Id };

        db.Users.AddRange(
            uAdmin, uDemo,
            uKom1, uKom2, uKom3,
            uRMgr1, uRMgr2,
            uROwn1, uROwn2, uROwn3, uROwn4,
            uDenMgr, uDen1, uDen2,
            uFOwn1, uFOwn2, uFOwn3,
            uEth1, uEth2,
            uUser1, uUser2, uUser3, uUser4, uUser5
        );
        await db.SaveChangesAsync();

        db.UserRoles.AddRange(
            new UserRole { UserId=uAdmin.Id,  RoleName="admin" },
            new UserRole { UserId=uDemo.Id,   RoleName="admin" },
            new UserRole { UserId=uKom1.Id,   RoleName="committee" },
            new UserRole { UserId=uKom2.Id,   RoleName="committee" },
            new UserRole { UserId=uKom3.Id,   RoleName="committee" },
            new UserRole { UserId=uRMgr1.Id,  RoleName="risk_manager" },
            new UserRole { UserId=uRMgr2.Id,  RoleName="risk_manager" },
            new UserRole { UserId=uROwn1.Id,  RoleName="risk_owner" },
            new UserRole { UserId=uROwn2.Id,  RoleName="risk_owner" },
            new UserRole { UserId=uROwn3.Id,  RoleName="risk_owner" },
            new UserRole { UserId=uROwn4.Id,  RoleName="risk_owner" },
            new UserRole { UserId=uDenMgr.Id, RoleName="audit_manager" },
            new UserRole { UserId=uDen1.Id,   RoleName="auditor" },
            new UserRole { UserId=uDen2.Id,   RoleName="auditor" },
            new UserRole { UserId=uFOwn1.Id,  RoleName="finding_owner" },
            new UserRole { UserId=uFOwn2.Id,  RoleName="finding_owner" },
            new UserRole { UserId=uFOwn3.Id,  RoleName="finding_owner" },
            new UserRole { UserId=uEth1.Id,   RoleName="ethics_board" },
            new UserRole { UserId=uEth2.Id,   RoleName="ethics_board" },
            new UserRole { UserId=uUser1.Id,  RoleName="user" },
            new UserRole { UserId=uUser2.Id,  RoleName="user" },
            new UserRole { UserId=uUser3.Id,  RoleName="user" },
            new UserRole { UserId=uUser4.Id,  RoleName="user" },
            new UserRole { UserId=uUser5.Id,  RoleName="user" }
        );

        db.UserCompanies.AddRange(
            new UserCompany { UserId=uAdmin.Id,  CompanyId=holding.Id },
            new UserCompany { UserId=uDemo.Id,   CompanyId=holding.Id },
            new UserCompany { UserId=uKom1.Id,   CompanyId=holding.Id },
            new UserCompany { UserId=uKom2.Id,   CompanyId=finAŞ.Id },
            new UserCompany { UserId=uKom3.Id,   CompanyId=finAŞ.Id },
            new UserCompany { UserId=uRMgr1.Id,  CompanyId=finAŞ.Id },
            new UserCompany { UserId=uRMgr2.Id,  CompanyId=finAŞ.Id },
            new UserCompany { UserId=uROwn1.Id,  CompanyId=finAŞ.Id },
            new UserCompany { UserId=uROwn2.Id,  CompanyId=tekAŞ.Id },
            new UserCompany { UserId=uROwn3.Id,  CompanyId=finAŞ.Id },
            new UserCompany { UserId=uROwn4.Id,  CompanyId=holding.Id },
            new UserCompany { UserId=uDenMgr.Id, CompanyId=holding.Id },
            new UserCompany { UserId=uDen1.Id,   CompanyId=holding.Id },
            new UserCompany { UserId=uDen2.Id,   CompanyId=holding.Id },
            new UserCompany { UserId=uFOwn1.Id,  CompanyId=finAŞ.Id },
            new UserCompany { UserId=uFOwn2.Id,  CompanyId=tekAŞ.Id },
            new UserCompany { UserId=uFOwn3.Id,  CompanyId=holding.Id },
            new UserCompany { UserId=uEth1.Id,   CompanyId=finAŞ.Id },
            new UserCompany { UserId=uEth2.Id,   CompanyId=holding.Id }
        );
        db.UserOrganizations.AddRange(
            new UserOrganization { UserId=uAdmin.Id,  OrganizationId=orgGMD.Id },
            new UserOrganization { UserId=uKom1.Id,   OrganizationId=orgGMD.Id },
            new UserOrganization { UserId=uKom2.Id,   OrganizationId=orgKB.Id },
            new UserOrganization { UserId=uKom3.Id,   OrganizationId=orgRisk.Id },
            new UserOrganization { UserId=uRMgr1.Id,  OrganizationId=orgRisk.Id },
            new UserOrganization { UserId=uRMgr2.Id,  OrganizationId=orgRisk.Id },
            new UserOrganization { UserId=uROwn1.Id,  OrganizationId=orgKB.Id },
            new UserOrganization { UserId=uROwn2.Id,  OrganizationId=orgInf.Id },
            new UserOrganization { UserId=uROwn3.Id,  OrganizationId=orgKB.Id },
            new UserOrganization { UserId=uROwn4.Id,  OrganizationId=orgGMD.Id },
            new UserOrganization { UserId=uDenMgr.Id, OrganizationId=orgGMD.Id },
            new UserOrganization { UserId=uDen1.Id,   OrganizationId=orgGMD.Id },
            new UserOrganization { UserId=uDen2.Id,   OrganizationId=orgGMD.Id },
            new UserOrganization { UserId=uFOwn1.Id,  OrganizationId=orgKB.Id },
            new UserOrganization { UserId=uFOwn2.Id,  OrganizationId=orgInf.Id },
            new UserOrganization { UserId=uFOwn3.Id,  OrganizationId=orgGMD.Id },
            new UserOrganization { UserId=uEth1.Id,   OrganizationId=orgRisk.Id },
            new UserOrganization { UserId=uEth2.Id,   OrganizationId=orgGMD.Id }
        );
        db.UserDepartments.AddRange(
            new UserDepartment { UserId=uAdmin.Id,  DepartmentId=dYK.Id },
            new UserDepartment { UserId=uKom1.Id,   DepartmentId=dYK.Id },
            new UserDepartment { UserId=uKom2.Id,   DepartmentId=dFin.Id },
            new UserDepartment { UserId=uKom3.Id,   DepartmentId=dRisk.Id },
            new UserDepartment { UserId=uRMgr1.Id,  DepartmentId=dRisk.Id },
            new UserDepartment { UserId=uRMgr2.Id,  DepartmentId=dRisk.Id },
            new UserDepartment { UserId=uROwn1.Id,  DepartmentId=dOps.Id },
            new UserDepartment { UserId=uROwn2.Id,  DepartmentId=dBT.Id },
            new UserDepartment { UserId=uROwn3.Id,  DepartmentId=dFin.Id },
            new UserDepartment { UserId=uROwn4.Id,  DepartmentId=dIK.Id },
            new UserDepartment { UserId=uDenMgr.Id, DepartmentId=dDen.Id },
            new UserDepartment { UserId=uDen1.Id,   DepartmentId=dDen.Id },
            new UserDepartment { UserId=uDen2.Id,   DepartmentId=dDen.Id },
            new UserDepartment { UserId=uFOwn1.Id,  DepartmentId=dFin.Id },
            new UserDepartment { UserId=uFOwn2.Id,  DepartmentId=dBT.Id },
            new UserDepartment { UserId=uFOwn3.Id,  DepartmentId=dSatın.Id },
            new UserDepartment { UserId=uEth1.Id,   DepartmentId=dHukuk.Id },
            new UserDepartment { UserId=uEth2.Id,   DepartmentId=dIK.Id }
        );
        await db.SaveChangesAsync();

        // ─── Riskler ─────────────────────────────────────────────────────────
        var t = DateTime.UtcNow;
        var risks = new[]
        {
            // 1 — proposed (yeni öneri)
            new Risk { Code="R-2026-001", Title="Kritik Müşteri Verisi İhlali",
                Category="Bilgi Teknolojileri", Status="proposed",
                ProposedById=uROwn2.Id, DepartmentId=dBT.Id, OrganizationId=orgInf.Id,
                Description="Müşteri kişisel verilerinin yetkisiz kişilerce erişilmesi ve sızdırılması riski.",
                Hazard="Zayıf erişim kontrolleri, şifreleme eksikliği",
                PossibleImpact="KVKK cezaları, itibar kaybı, müşteri güveni erozyonu",
                ProposedAt=t.AddDays(-5) },

            // 2 — review (komite incelemesinde)
            new Risk { Code="R-2026-002", Title="Tedarik Zinciri Kesintisi",
                Category="Operasyonel", Status="review",
                ProposedById=uROwn1.Id, OwnerId=uROwn1.Id, DepartmentId=dOps.Id, OrganizationId=orgKB.Id,
                Description="Kritik tedarikçilerin operasyonlarını durdurması veya gecikmeli teslimat.",
                Hazard="Tek tedarikçi bağımlılığı, coğrafi konsantrasyon",
                PossibleImpact="Üretim durması, gelir kaybı, sözleşme ihlalleri",
                ProposedAt=t.AddDays(-15) },

            // 3 — approved (onaylandı, değerlendirme yapıldı)
            new Risk { Code="R-2026-003", Title="Döviz Kuru Dalgalanması",
                Category="Finansal", Status="approved",
                ProposedById=uRMgr1.Id, OwnerId=uFOwn1.Id, DepartmentId=dFin.Id, OrganizationId=orgHaz.Id,
                RiskStrategy="Riski Transfer Et",
                Description="Yabancı para cinsinden yükümlülüklerde kur değişikliklerinin yarattığı finansal etki.",
                Hazard="Dolar/TL paritesindeki volatilite",
                PossibleImpact="Finansal tablolarda değer kaybı, faiz gider artışı",
                ProposedAt=t.AddDays(-30) },

            // 4 — action_planned (kontrol altına alınıyor)
            new Risk { Code="R-2026-004", Title="BT Altyapısında Güvenlik Açıkları",
                Category="Bilgi Teknolojileri", Status="action_planned",
                ProposedById=uROwn2.Id, OwnerId=uROwn2.Id, DepartmentId=dSiber.Id, OrganizationId=orgInf.Id,
                RiskStrategy="Riski Azalt",
                Description="Sunucu sistemlerinde tespit edilen kritik güvenlik açıkları ve yama eksiklikleri.",
                Hazard="Güncellenmemiş sistemler, yama yönetimi yetersizliği",
                PossibleImpact="Siber saldırı, sistem ele geçirme, veri çalınması",
                ProposedAt=t.AddDays(-45) },

            // 5 — controlled (tamamen kontrol altında)
            new Risk { Code="R-2026-005", Title="KVKK Uyum Eksikliği",
                Category="Uyum", Status="controlled",
                ProposedById=uRMgr2.Id, OwnerId=uEth1.Id, DepartmentId=dHukuk.Id, OrganizationId=orgRisk.Id,
                RiskStrategy="Riski Azalt",
                Description="Kişisel Verilerin Korunması Kanunu kapsamında eksik uyum uygulamaları.",
                Hazard="Güncel mevzuat gerekliliklerine uyumsuzluk",
                PossibleImpact="İdari para cezaları, şikâyet süreçleri",
                ProposedAt=t.AddDays(-90) },

            // 6 — rejected (reddedildi)
            new Risk { Code="R-2026-006", Title="Ofis Taşınma Projesi Gecikme Riski",
                Category="Operasyonel", Status="rejected",
                ProposedById=uUser1.Id, DepartmentId=dOps.Id, OrganizationId=orgKB.Id,
                RejectionReason="Risk kabul eşiğinin altında, mevcut proje yönetim süreçleri yeterli.",
                Description="Ofis taşınma sürecinde yaşanabilecek hizmet kesintileri.",
                ProposedAt=t.AddDays(-20) },

            // 7 — proposed (yeni)
            new Risk { Code="R-2026-007", Title="Yüksek Personel Devir Hızı",
                Category="İnsan Kaynakları", Status="proposed",
                ProposedById=uUser2.Id, DepartmentId=dIK.Id, OrganizationId=orgGMD.Id,
                Description="Kilit pozisyonlarda aşırı personel sirkülasyonu ve bilgi kaybı riski.",
                Hazard="Yetersiz ücret politikası, kariyer gelişim eksikliği",
                PossibleImpact="Operasyonel süreklilik tehlikesi, işe alım maliyetleri",
                ProposedAt=t.AddDays(-3) },

            // 8 — approved (onaylı, değerlendirmeli)
            new Risk { Code="R-2026-008", Title="Hedefli Siber Saldırı (APT)",
                Category="Bilgi Teknolojileri", Status="approved",
                ProposedById=uROwn2.Id, OwnerId=uROwn2.Id, DepartmentId=dSiber.Id, OrganizationId=orgInf.Id,
                RiskStrategy="Riski Azalt",
                Description="Gelişmiş kalıcı tehdit aktörlerinin altyapıya yönelik uzun süreli sızma girişimleri.",
                Hazard="Devlet destekli hacker grupları, sektöre özgü hedefleme",
                PossibleImpact="Kritik sistemlerin ele geçirilmesi, veri hırsızlığı",
                ProposedAt=t.AddDays(-60) },

            // 9 — action_planned (aksiyonlar devam ediyor)
            new Risk { Code="R-2026-009", Title="Operasyonel Süreç Hatası",
                Category="Operasyonel", Status="action_planned",
                ProposedById=uROwn1.Id, OwnerId=uROwn1.Id, DepartmentId=dOps.Id, OrganizationId=orgKB.Id,
                RiskStrategy="Riski Azalt",
                Description="Manuel süreçlerde insan hatası kaynaklı hatalı işlem yapılması.",
                Hazard="Otomasyon eksikliği, yetersiz kontrol mekanizmaları",
                PossibleImpact="Mali kayıp, müşteri memnuniyetsizliği",
                ProposedAt=t.AddDays(-50) },

            // 10 — controlled
            new Risk { Code="R-2026-010", Title="Kredi Geri Dönmeme Riski",
                Category="Finansal", Status="controlled",
                ProposedById=uRMgr1.Id, OwnerId=uFOwn1.Id, DepartmentId=dFin.Id, OrganizationId=orgKB.Id,
                RiskStrategy="Riski Azalt",
                Description="Kurumsal kredi müşterilerinin yükümlülüklerini yerine getirememesi.",
                Hazard="Ekonomik daralma, sektör özgü kriz",
                PossibleImpact="Karşılık giderleri, sermaye yeterliliği baskısı",
                ProposedAt=t.AddDays(-120) },

            // 11 — proposed
            new Risk { Code="R-2026-011", Title="Yeni Bankacılık Regülasyonu",
                Category="Uyum", Status="proposed",
                ProposedById=uEth1.Id, DepartmentId=dHukuk.Id, OrganizationId=orgRisk.Id,
                Description="BDDK'nın yayımladığı yeni sermaye yeterliliği yönetmeliği uyum gereklilikleri.",
                Hazard="Kısa uygulama süresi, yüksek teknik gereksinim",
                PossibleImpact="Ek sermaye ihtiyacı, operasyonel değişiklik maliyeti",
                ProposedAt=t.AddDays(-2) },

            // 12 — approved
            new Risk { Code="R-2026-012", Title="Deprem ve Doğal Afet Riski",
                Category="Operasyonel", Status="approved",
                ProposedById=uRMgr2.Id, OwnerId=uDenMgr.Id, DepartmentId=dOps.Id, OrganizationId=orgGMD.Id,
                RiskStrategy="Riski Kabul Et",
                Description="Doğal afet kaynaklı fiziksel hasar ve iş sürekliliği kesintisi.",
                Hazard="Deprem kuşağında yer alan ofis binaları",
                PossibleImpact="Fiziksel hasar, personel güvenliği, veri kaybı",
                ProposedAt=t.AddDays(-40) },
        };
        db.Risks.AddRange(risks);
        await db.SaveChangesAsync();

        // ─── Değerlendirmeler ─────────────────────────────────────────────────
        // Risk 3 (Döviz Kuru) — approved
        db.Evaluations.Add(new Evaluation { RiskId=risks[2].Id, EvalType="initial",
            Probability=3, Exposure=6, Consequence=40, Score=720, RiskLevel="Çok Yüksek Risk", EvaluatedById=uKom1.Id });
        db.Evaluations.Add(new Evaluation { RiskId=risks[2].Id, EvalType="residual",
            Probability=2, Exposure=3, Consequence=15, Score=90,  RiskLevel="Orta Risk",       EvaluatedById=uKom1.Id });

        // Risk 4 (BT Güvenlik Açıkları) — action_planned
        db.Evaluations.Add(new Evaluation { RiskId=risks[3].Id, EvalType="initial",
            Probability=6, Exposure=6, Consequence=40, Score=1440, RiskLevel="Çok Yüksek Risk", EvaluatedById=uKom1.Id });
        db.Evaluations.Add(new Evaluation { RiskId=risks[3].Id, EvalType="residual",
            Probability=3, Exposure=3, Consequence=15, Score=135,  RiskLevel="Yüksek Risk",     EvaluatedById=uKom1.Id });

        // Risk 5 (KVKK) — controlled
        db.Evaluations.Add(new Evaluation { RiskId=risks[4].Id, EvalType="initial",
            Probability=6, Exposure=3, Consequence=15, Score=270, RiskLevel="Yüksek Risk", EvaluatedById=uKom1.Id });
        db.Evaluations.Add(new Evaluation { RiskId=risks[4].Id, EvalType="residual",
            Probability=1, Exposure=2, Consequence=7,  Score=14,  RiskLevel="Düşük Risk",  EvaluatedById=uKom1.Id });

        // Risk 8 (APT Siber) — approved
        db.Evaluations.Add(new Evaluation { RiskId=risks[7].Id, EvalType="initial",
            Probability=3, Exposure=6, Consequence=40, Score=720, RiskLevel="Çok Yüksek Risk", EvaluatedById=uKom1.Id });

        // Risk 9 (Operasyonel Hata) — action_planned
        db.Evaluations.Add(new Evaluation { RiskId=risks[8].Id, EvalType="initial",
            Probability=6, Exposure=3, Consequence=15, Score=270, RiskLevel="Yüksek Risk",     EvaluatedById=uKom1.Id });
        db.Evaluations.Add(new Evaluation { RiskId=risks[8].Id, EvalType="residual",
            Probability=3, Exposure=2, Consequence=7,  Score=42,  RiskLevel="Düşük Risk",      EvaluatedById=uKom1.Id });

        // Risk 10 (Kredi) — controlled
        db.Evaluations.Add(new Evaluation { RiskId=risks[9].Id, EvalType="initial",
            Probability=3, Exposure=6, Consequence=40, Score=720, RiskLevel="Çok Yüksek Risk", EvaluatedById=uKom1.Id });
        db.Evaluations.Add(new Evaluation { RiskId=risks[9].Id, EvalType="residual",
            Probability=1, Exposure=3, Consequence=15, Score=45,  RiskLevel="Düşük Risk",      EvaluatedById=uKom1.Id });

        // Risk 12 (Deprem) — approved
        db.Evaluations.Add(new Evaluation { RiskId=risks[11].Id, EvalType="initial",
            Probability=2, Exposure=6, Consequence=40, Score=480, RiskLevel="Çok Yüksek Risk", EvaluatedById=uKom1.Id });

        // ─── Kontroller ───────────────────────────────────────────────────────
        db.Controls.AddRange(
            // Risk 4 — BT
            new Control { RiskId=risks[3].Id, Description="Yama yönetimi sistemi kurulumu ve düzenli güncelleme takvimi.",
                ControlType="Önleyici", Effectiveness="Tatmin Edici", Frequency="Aylık", EnteredById=uROwn2.Id },
            new Control { RiskId=risks[3].Id, Description="Penetrasyon testi ve zafiyet taraması.",
                ControlType="Tespit Edici", Effectiveness="Gelişmekte", Frequency="3 Aylık", EnteredById=uRMgr2.Id },

            // Risk 5 — KVKK
            new Control { RiskId=risks[4].Id, Description="Veri envanteri oluşturuldu ve periyodik güncelleme prosedürü tanımlandı.",
                ControlType="Önleyici", Effectiveness="Tatmin Edici", Frequency="6 Aylık", EnteredById=uEth1.Id },
            new Control { RiskId=risks[4].Id, Description="Çalışan KVKK farkındalık eğitimi zorunlu kılındı.",
                ControlType="Önleyici", Effectiveness="Tatmin Edici", Frequency="Yıllık", EnteredById=uRMgr1.Id },
            new Control { RiskId=risks[4].Id, Description="DPO (Veri Koruma Görevlisi) atandı.",
                ControlType="Önleyici", Effectiveness="Tatmin Edici", Frequency="Sürekli", EnteredById=uRMgr1.Id },

            // Risk 8 — APT Siber
            new Control { RiskId=risks[7].Id, Description="SOC (Güvenlik Operasyon Merkezi) 7/24 izleme.",
                ControlType="Tespit Edici", Effectiveness="Tatmin Edici", Frequency="Sürekli", EnteredById=uROwn2.Id },
            new Control { RiskId=risks[7].Id, Description="Çok faktörlü kimlik doğrulama zorunluluğu.",
                ControlType="Önleyici", Effectiveness="Tatmin Edici", Frequency="Sürekli", EnteredById=uROwn2.Id },

            // Risk 9 — Operasyonel Hata
            new Control { RiskId=risks[8].Id, Description="4-gözlü prensip: kritik işlemlerde çift onay zorunluluğu.",
                ControlType="Önleyici", Effectiveness="Tatmin Edici", Frequency="Her İşlemde", EnteredById=uROwn1.Id },

            // Risk 10 — Kredi
            new Control { RiskId=risks[9].Id, Description="Kredi risk modeli güncellendi, LTV limitleri sıkılaştırıldı.",
                ControlType="Önleyici", Effectiveness="Tatmin Edici", Frequency="Sürekli", EnteredById=uFOwn1.Id },
            new Control { RiskId=risks[9].Id, Description="Sektör konsantrasyon limitleri revize edildi.",
                ControlType="Önleyici", Effectiveness="Tatmin Edici", Frequency="3 Aylık", EnteredById=uRMgr1.Id }
        );

        // ─── Aksiyon Planları ─────────────────────────────────────────────────
        db.ActionPlans.AddRange(
            // Risk 4 — BT Güvenlik Açıkları
            new ActionPlan { RiskId=risks[3].Id, Description="Tüm sunucuların işletim sistemi yamalarının uygulanması",
                Responsible="Bilgi Teknolojileri Ekibi", Status="in_progress",
                DueDate=DateOnly.FromDateTime(t.AddDays(14)), CreatedById=uROwn2.Id },
            new ActionPlan { RiskId=risks[3].Id, Description="Ağ segmentasyonu ve güvenlik duvarı kurallarının güncellenmesi",
                Responsible="Siber Güvenlik Ekibi", Status="planned",
                DueDate=DateOnly.FromDateTime(t.AddDays(30)), CreatedById=uROwn2.Id },
            new ActionPlan { RiskId=risks[3].Id, Description="Endpoint güvenlik yazılımının tüm iş istasyonlarına kurulumu",
                Responsible="BT Operasyon", Status="completed",
                DueDate=DateOnly.FromDateTime(t.AddDays(-10)), CompletedAt=t.AddDays(-5), CreatedById=uROwn2.Id },

            // Risk 9 — Operasyonel Hata
            new ActionPlan { RiskId=risks[8].Id, Description="Kritik süreçlerin RPA (Robot Süreç Otomasyonu) ile otomatikleştirilmesi",
                Responsible="Operasyon & BT", Status="planned",
                DueDate=DateOnly.FromDateTime(t.AddDays(60)), CreatedById=uROwn1.Id },
            new ActionPlan { RiskId=risks[8].Id, Description="Süreç dokümantasyonu ve prosedür kılavuzlarının güncellenmesi",
                Responsible="Operasyon Yöneticisi", Status="in_progress",
                DueDate=DateOnly.FromDateTime(t.AddDays(7)), CreatedById=uROwn1.Id },
            new ActionPlan { RiskId=risks[8].Id, Description="Çalışan hata analizi eğitimlerinin düzenlenmesi",
                Responsible="İnsan Kaynakları", Status="completed",
                DueDate=DateOnly.FromDateTime(t.AddDays(-20)), CompletedAt=t.AddDays(-15), CreatedById=uROwn1.Id }
        );
        await db.SaveChangesAsync();

        // ─── İç Denetim Planı ─────────────────────────────────────────────────
        var auditPlan = new AuditPlan { Year=2026, Title="2026 Yılı İç Denetim Planı",
            Description="Tüm kritik süreçleri kapsayan yıllık denetim programı.", CreatedById=uDenMgr.Id };
        db.AuditPlans.Add(auditPlan);
        await db.SaveChangesAsync();

        db.AuditPlanItems.AddRange(
            new AuditPlanItem { AuditPlanId=auditPlan.Id, Title="Q1 — Mali İşlemler Denetimi",
                AuditType="ordinary", AuditedUnit="Finans & Muhasebe",
                PlannedStartDate=new DateOnly(2026,1,15), PlannedEndDate=new DateOnly(2026,3,31),
                ActualStartDate=new DateOnly(2026,1,20), ActualEndDate=new DateOnly(2026,3,28),
                DepartmentId=dFin.Id, ResponsibleId=uDen1.Id, SortOrder=1 },
            new AuditPlanItem { AuditPlanId=auditPlan.Id, Title="Q2 — BT Sistemleri Denetimi",
                AuditType="it_audit", AuditedUnit="Bilgi Teknolojileri",
                PlannedStartDate=new DateOnly(2026,4,1), PlannedEndDate=new DateOnly(2026,6,30),
                ActualStartDate=new DateOnly(2026,4,5), DepartmentId=dBT.Id,
                ResponsibleId=uDen1.Id, SortOrder=2 },
            new AuditPlanItem { AuditPlanId=auditPlan.Id, Title="Q3 — İK Süreçleri Denetimi",
                AuditType="ordinary", AuditedUnit="İnsan Kaynakları",
                PlannedStartDate=new DateOnly(2026,7,1), PlannedEndDate=new DateOnly(2026,9,30),
                DepartmentId=dIK.Id, ResponsibleId=uDenMgr.Id, SortOrder=3 },
            new AuditPlanItem { AuditPlanId=auditPlan.Id, Title="Q4 — Satın Alma Süreçleri Denetimi",
                AuditType="ordinary", AuditedUnit="Satın Alma",
                PlannedStartDate=new DateOnly(2026,10,1), PlannedEndDate=new DateOnly(2026,12,31),
                DepartmentId=dSatın.Id, ResponsibleId=uDen1.Id, SortOrder=4 }
        );
        await db.SaveChangesAsync();

        // ─── İç Denetimler ────────────────────────────────────────────────────
        var audit1 = new InternalAudit { Code="ID-2026-001", Title="Q1 Mali İşlemler Denetimi",
            AuditType="ordinary", AuditedUnit="Finans & Muhasebe",
            Period="2026 Q1", Status="completed",
            StartDate=new DateOnly(2026,1,20), EndDate=new DateOnly(2026,3,28),
            LeadAuditorId=uDen1.Id, CreatedAt=t.AddDays(-90) };
        var audit2 = new InternalAudit { Code="ID-2026-002", Title="Q2 BT Sistemleri Denetimi",
            AuditType="it_audit", AuditedUnit="Bilgi Teknolojileri & Siber Güvenlik",
            Period="2026 Q2", Status="in_progress",
            StartDate=new DateOnly(2026,4,5),
            LeadAuditorId=uDen1.Id, CreatedAt=t.AddDays(-30) };
        var audit3 = new InternalAudit { Code="ID-2026-003", Title="Q3 İK Süreçleri Denetimi",
            AuditType="ordinary", AuditedUnit="İnsan Kaynakları",
            Period="2026 Q3", Status="planned",
            StartDate=new DateOnly(2026,7,1),
            LeadAuditorId=uDenMgr.Id, CreatedAt=t.AddDays(-10) };
        var audit4 = new InternalAudit { Code="ID-2025-004", Title="2025 Satın Alma Süreçleri Denetimi",
            AuditType="ordinary", AuditedUnit="Satın Alma",
            Period="2025 Y2", Status="completed",
            StartDate=new DateOnly(2025,9,1), EndDate=new DateOnly(2025,12,15),
            LeadAuditorId=uDenMgr.Id, CreatedAt=t.AddDays(-180) };
        db.InternalAudits.AddRange(audit1, audit2, audit3, audit4);
        await db.SaveChangesAsync();

        // ─── Bulgular ─────────────────────────────────────────────────────────
        // Denetim 1 (tamamlandı) — hepsi kapalı
        var f1 = new AuditFinding { Code="B-2026-001", Title="Onaysız ödeme talimatları tespit edildi",
            Category="Mali", Severity="Yüksek", AuditPeriod="2026 Q1",
            AuditorId=uDen1.Id, OwnerId=uFOwn1.Id, InternalAuditId=audit1.Id,
            Status="closed", ClosedAt=t.AddDays(-20),
            DueDate=new DateOnly(2026,3,15), AuditSource="internal",
            Description="Onay limiti üzerindeki ödemeler yetkisiz personel tarafından gerçekleştirilmiştir." };
        var f2 = new AuditFinding { Code="B-2026-002", Title="Banka mutabakat gecikmesi",
            Category="Mali", Severity="Orta", AuditPeriod="2026 Q1",
            AuditorId=uDen1.Id, OwnerId=uFOwn1.Id, InternalAuditId=audit1.Id,
            Status="closed", ClosedAt=t.AddDays(-25),
            DueDate=new DateOnly(2026,3,31), AuditSource="internal",
            Description="Aylık banka mutabakatları zamanında kapatılmamaktadır." };
        var f3 = new AuditFinding { Code="B-2026-003", Title="Fatura doğrulama sürecinde kontrol eksikliği",
            Category="Mali", Severity="Düşük", AuditPeriod="2026 Q1",
            AuditorId=uDen1.Id, OwnerId=uFOwn1.Id, InternalAuditId=audit1.Id,
            Status="closed", ClosedAt=t.AddDays(-15),
            DueDate=new DateOnly(2026,4,15), AuditSource="internal" };
        var f4 = new AuditFinding { Code="B-2026-004", Title="Çift ödeme tespiti",
            Category="Mali", Severity="Yüksek", AuditPeriod="2026 Q1",
            AuditorId=uDen1.Id, OwnerId=uFOwn1.Id, InternalAuditId=audit1.Id,
            Status="closed", ClosedAt=t.AddDays(-30),
            DueDate=new DateOnly(2026,3,10), AuditSource="internal" };

        // Denetim 2 (devam ediyor) — farklı durumlar
        var f5 = new AuditFinding { Code="B-2026-005", Title="Güncellenmemiş erişim yetkileri",
            Category="BT Güvenliği", Severity="Kritik", AuditPeriod="2026 Q2",
            AuditorId=uDen1.Id, OwnerId=uROwn2.Id, InternalAuditId=audit2.Id,
            Status="open", DueDate=DateOnly.FromDateTime(t.AddDays(21)), AuditSource="internal",
            Description="Ayrılan personelin sistem erişimleri 30 günden uzun süre kapatılmamıştır." };
        var f6 = new AuditFinding { Code="B-2026-006", Title="Yedekleme prosedürü eksikliği",
            Category="BT Operasyon", Severity="Yüksek", AuditPeriod="2026 Q2",
            AuditorId=uDen1.Id, OwnerId=uROwn2.Id, InternalAuditId=audit2.Id,
            Status="closure_requested", DueDate=DateOnly.FromDateTime(t.AddDays(5)), AuditSource="internal",
            Description="Kritik veritabanlarının günlük yedeklemesi düzenli test edilmemektedir." };
        var f7 = new AuditFinding { Code="B-2026-007", Title="Lisanssız yazılım kullanımı",
            Category="BT Uyum", Severity="Orta", AuditPeriod="2026 Q2",
            AuditorId=uDen1.Id, OwnerId=uROwn2.Id, InternalAuditId=audit2.Id,
            Status="open", DueDate=DateOnly.FromDateTime(t.AddDays(45)), AuditSource="internal" };
        var f8 = new AuditFinding { Code="B-2026-008", Title="Felaket kurtarma planı güncel değil",
            Category="İş Sürekliliği", Severity="Yüksek", AuditPeriod="2026 Q2",
            AuditorId=uDen1.Id, OwnerId=uROwn2.Id, InternalAuditId=audit2.Id,
            Status="open", DueDate=DateOnly.FromDateTime(t.AddDays(3)), AuditSource="internal",
            Description="DRP son güncelleme tarihi 18 ay öncesidir; test kaydı bulunmamaktadır." };
        var f9 = new AuditFinding { Code="B-2026-009", Title="Şifre politikası ihlalleri",
            Category="BT Güvenliği", Severity="Orta", AuditPeriod="2026 Q2",
            AuditorId=uDen1.Id, OwnerId=uROwn2.Id, InternalAuditId=audit2.Id,
            Status="closed", ClosedAt=t.AddDays(-5),
            DueDate=DateOnly.FromDateTime(t.AddDays(-10)), AuditSource="internal" };

        // Denetim 3 (planlı) — erken aşama
        var f10 = new AuditFinding { Code="B-2026-010", Title="İşe alım sürecinde referans kontrol eksikliği",
            Category="İK Süreçleri", Severity="Orta", AuditPeriod="2026 Q3",
            AuditorId=uDenMgr.Id, OwnerId=uUser2.Id, InternalAuditId=audit3.Id,
            Status="open", DueDate=DateOnly.FromDateTime(t.AddDays(90)), AuditSource="internal" };
        var f11 = new AuditFinding { Code="B-2026-011", Title="Performans değerlendirme tutarsızlıkları",
            Category="İK Süreçleri", Severity="Düşük", AuditPeriod="2026 Q3",
            AuditorId=uDenMgr.Id, OwnerId=uUser2.Id, InternalAuditId=audit3.Id,
            Status="open", DueDate=DateOnly.FromDateTime(t.AddDays(105)), AuditSource="internal" };

        // Denetim 4 (tamamlandı) — hepsi kapalı
        var f12 = new AuditFinding { Code="B-2025-001", Title="Tedarikçi fiyat karşılaştırması yapılmamış",
            Category="Satın Alma", Severity="Yüksek", AuditPeriod="2025 Y2",
            AuditorId=uDenMgr.Id, OwnerId=uROwn1.Id, InternalAuditId=audit4.Id,
            Status="closed", ClosedAt=t.AddDays(-60),
            DueDate=new DateOnly(2025,12,1), AuditSource="internal" };
        var f13 = new AuditFinding { Code="B-2025-002", Title="Sözleşme takip sürecinde gecikmeler",
            Category="Satın Alma", Severity="Orta", AuditPeriod="2025 Y2",
            AuditorId=uDenMgr.Id, OwnerId=uROwn1.Id, InternalAuditId=audit4.Id,
            Status="closed", ClosedAt=t.AddDays(-55),
            DueDate=new DateOnly(2025,12,15), AuditSource="internal" };
        var f14 = new AuditFinding { Code="B-2025-003", Title="Onay matriksine aykırı alımlar",
            Category="Satın Alma", Severity="Kritik", AuditPeriod="2025 Y2",
            AuditorId=uDenMgr.Id, OwnerId=uROwn1.Id, InternalAuditId=audit4.Id,
            Status="closed", ClosedAt=t.AddDays(-70),
            DueDate=new DateOnly(2025,11,30), AuditSource="internal" };
        var f15 = new AuditFinding { Code="B-2025-004", Title="Tedarikçi puanlama sistemi oluşturulmamış",
            Category="Satın Alma", Severity="Düşük", AuditPeriod="2025 Y2",
            AuditorId=uDenMgr.Id, OwnerId=uROwn1.Id, InternalAuditId=audit4.Id,
            Status="closed", ClosedAt=t.AddDays(-50),
            DueDate=new DateOnly(2026,1,31), AuditSource="internal" };

        db.AuditFindings.AddRange(f1,f2,f3,f4,f5,f6,f7,f8,f9,f10,f11,f12,f13,f14,f15);
        await db.SaveChangesAsync();

        // Kapatma başvurusu — f6
        db.ClosureRequests.Add(new ClosureRequest
        {
            FindingId=f6.Id, Description="Yedekleme prosedürü revize edildi ve test yapıldı.",
            Evidence="Yedek test raporu eklendi.", RequestedById=uFOwn1.Id,
        });

        // Risk-Bulgu bağlantısı
        db.RiskFindingLinks.AddRange(
            new RiskFindingLink { RiskId=risks[3].Id, FindingId=f5.Id, CreatedById=uDen1.Id },
            new RiskFindingLink { RiskId=risks[3].Id, FindingId=f6.Id, CreatedById=uDen1.Id },
            new RiskFindingLink { RiskId=risks[8].Id, FindingId=f1.Id, CreatedById=uDen1.Id }
        );

        // ─── Dış Denetim Kurumları & Denetimler ───────────────────────────────
        db.ExternalAuditBodies.AddRange(
            new ExternalAuditBody { Name="BDO Dış Denetim A.Ş.", IsActive=true, SortOrder=1 },
            new ExternalAuditBody { Name="BDDK",                  IsActive=true, SortOrder=2 },
            new ExternalAuditBody { Name="Maliye Bakanlığı",       IsActive=true, SortOrder=3 }
        );

        db.UserExternalAuditBodies.AddRange(
            new UserExternalAuditBody { UserId=uDenMgr.Id, AuditingBody="BDO Dış Denetim A.Ş.", CanEdit=true },
            new UserExternalAuditBody { UserId=uDenMgr.Id, AuditingBody="BDDK",                  CanEdit=false },
            new UserExternalAuditBody { UserId=uDen1.Id,   AuditingBody="BDO Dış Denetim A.Ş.", CanEdit=false }
        );

        db.ExternalAudits.AddRange(
            new ExternalAudit { Code="DA-2026-001",
                AuditingBody="BDO Dış Denetim A.Ş.", Subject="2025 Yılı Bağımsız Finansal Denetimi",
                AuditDate=new DateOnly(2026,1,10), EndDate=new DateOnly(2026,3,31),
                Status="completed", ResponsibleUserId=uDenMgr.Id, CreatedById=uDenMgr.Id,
                Notes="Denetim tamamlandı. Şartlı görüş verildi." },
            new ExternalAudit { Code="DA-2026-002",
                AuditingBody="BDDK", Subject="Sermaye Yeterliliği ve Kredi Riski Denetimi",
                AuditDate=new DateOnly(2026,4,15), Status="in_progress",
                ResponsibleUserId=uDenMgr.Id, CreatedById=uDenMgr.Id }
        );
        await db.SaveChangesAsync();

        // Dış denetim bulguları
        db.AuditFindings.AddRange(
            new AuditFinding { Code="DA-B-001", Title="Konsolidasyon sürecinde tutarsızlıklar",
                Category="Mali Raporlama", Severity="Yüksek", AuditPeriod="2025",
                AuditorId=uDenMgr.Id, OwnerId=uFOwn1.Id,
                Status="closed", ClosedAt=t.AddDays(-10),
                DueDate=new DateOnly(2026,4,30), AuditSource="external" },
            new AuditFinding { Code="DA-B-002", Title="Sermaye yeterliliği hesaplama hataları",
                Category="Regülatör Uyum", Severity="Kritik", AuditPeriod="2026",
                AuditorId=uDenMgr.Id, OwnerId=uRMgr1.Id,
                Status="open", DueDate=DateOnly.FromDateTime(t.AddDays(30)), AuditSource="external",
                Description="BDDK tarafından tespit edilen sermaye hesaplama metodoloji uyumsuzluğu." }
        );

        // ─── Etik Bildirimleri ────────────────────────────────────────────────
        db.EthicsReports.AddRange(
            // 1 — pending (bekliyor)
            new EthicsReport { Code="EB-2026-001",
                Subject="İşyerinde Psikolojik Taciz İddiası",
                Description="Belirli bir departmanda yönetici tarafından çalışanlara psikolojik baskı uygulandığına dair bildirim.",
                ReportCategory="Mobbing / Taciz", Status="pending",
                SubmittedAt=t.AddDays(-3) },

            // 2 — ethics_board_notified (denetim incelemesi tamamlandı)
            new EthicsReport { Code="EB-2026-002",
                Subject="Tedarikçi ile Çıkar Çatışması",
                Description="Satın alma sürecinde belirli bir tedarikçiye ayrıcalıklı muamele yapıldığına dair şüphe bildirimi.",
                ReportCategory="Yolsuzluk / Rüşvet", Status="ethics_board_notified",
                SubmittedAt=t.AddDays(-25),
                AuditDecision="investigate",
                AuditNotes="Dosya incelendi, ciddi bulgular mevcut. Etik Kurul'a iletildi.",
                AuditReviewedById=uDen1.Id, AuditReviewedAt=t.AddDays(-15) },

            // 3 — investigated (soruşturma aşamasında)
            new EthicsReport { Code="EB-2026-003",
                Subject="Finansal Usulsüzlük Bildirimi",
                Description="Muhasebe kayıtlarında gerçeği yansıtmayan fatura düzenlenmesi iddiası.",
                ReportCategory="Finansal Usulsüzlük", Status="investigated",
                SubmittedAt=t.AddDays(-40),
                AuditDecision="investigate",
                AuditNotes="Ön inceleme tamamlandı, soruşturma açıldı.",
                AuditReviewedById=uDenMgr.Id, AuditReviewedAt=t.AddDays(-30),
                EthicsDecision="investigated",
                EthicsNotes="Kurul soruşturma başlattı. Bulgular devam ediyor.",
                EthicsReviewedById=uEth1.Id, EthicsReviewedAt=t.AddDays(-20) },

            // 4 — completed (tamamlandı)
            new EthicsReport { Code="EB-2026-004",
                Subject="Veri Gizliliği İhlali Şikayeti",
                Description="Müşteri verilerinin yetkisiz kişilerle paylaşıldığına dair bildirim.",
                ReportCategory="Veri Gizliliği", Status="completed",
                SubmittedAt=t.AddDays(-90),
                AuditDecision="investigate",
                AuditReviewedById=uDen1.Id, AuditReviewedAt=t.AddDays(-80),
                EthicsDecision="completed",
                EthicsNotes="Soruşturma tamamlandı. Yetersiz delil. Vaka kapatıldı.",
                EthicsReviewedById=uEth1.Id, EthicsReviewedAt=t.AddDays(-60) },

            // 5 — pending (yeni)
            new EthicsReport { Code="EB-2026-005",
                Subject="Müşteri Bilgilerinin Kötüye Kullanımı",
                Description="Bir çalışanın müşteri iletişim bilgilerini kişisel amaçlarla kullandığı iddia ediliyor.",
                ReportCategory="Gizlilik İhlali", Status="pending",
                SubmittedAt=t.AddDays(-1) }
        );

        // ─── Counters (kod üretimi için) ──────────────────────────────────────
        db.Counters.AddRange(
            new Counter { Key=$"risk-{t.Year}",    Value=12 },
            new Counter { Key=$"finding-{t.Year}", Value=11 },
            new Counter { Key=$"ethics-{t.Year}",  Value=5 },
            new Counter { Key="audit-2026",         Value=3 },
            new Counter { Key="audit-2025",         Value=4 }
        );

        await db.SaveChangesAsync();

        Console.WriteLine("==========================================================");
        Console.WriteLine("  DEMO SEED v3 TAMAMLANDI");
        Console.WriteLine("==========================================================");
        Console.WriteLine("  Kullanıcılar (tümü Demo123!):");
        Console.WriteLine("  admin        : Admin123!");
        Console.WriteLine("  demo         : Demo123!");
        Console.WriteLine("  a.kaya       : Demo123!  (Risk Komitesi)");
        Console.WriteLine("  m.yilmaz     : Demo123!  (Risk Sahibi — Operasyon)");
        Console.WriteLine("  f.arslan     : Demo123!  (Risk Yöneticisi)");
        Console.WriteLine("  z.demir      : Demo123!  (Denetçi)");
        Console.WriteLine("  c.sahin      : Demo123!  (Denetim Müdürü)");
        Console.WriteLine("  a.celik      : Demo123!  (Risk Sahibi — BT)");
        Console.WriteLine("  e.kurt       : Demo123!  (Kullanıcı)");
        Console.WriteLine("  h.polat      : Demo123!  (Bulgu Sahibi)");
        Console.WriteLine("  s.yildiz     : Demo123!  (Etik Kurul)");
        Console.WriteLine("  b.ozkan      : Demo123!  (Risk Yöneticisi)");
        Console.WriteLine("  n.arslan     : Demo123!  (Kullanıcı — İK)");
        Console.WriteLine("==========================================================");
    }
}
