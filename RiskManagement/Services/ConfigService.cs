using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services.Ai;
using RiskManagement.Services.Email;

namespace RiskManagement.Services;

public class ConfigService(AppDbContext db, ILogger<ConfigService> logger,
    IDataProtectionProvider dataProtection)
{
    // Scoped servis olduğu için cache per-request'tir; yine de Blazor Server'ın
    // eş zamanlı bileşen güncellemelerine karşı ConcurrentDictionary kullanılıyor.
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly IDataProtector _protector = dataProtection.CreateProtector("ConfigService.AiApiKey");

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    // Varsayılan değerler: somut DTO/POCO'lar kullanılır; anonim tip yok.
    // Renk ve eşik bilgileri fk_levels içinde tutulur — ConfigService dışına sızmaz.
    private static readonly IReadOnlyDictionary<string, object> _defaults =
        new Dictionary<string, object>
        {
            ["risk_categories"]       = new[] { "Stratejik","Operasyonel","Finansal","Uyum","Bilgi Teknolojileri","İtibar","Çevre" },
            ["responsible_units"]     = new[] { "Tıbbi Koordinatörlük","İdari Koordinatörlük","Finansal Koordinatörlük","İnsan Kaynakları Direktörlüğü","Bilgi İşlem Direktörlüğü","Yönetim" },
            ["risk_strategies"]       = new[] { "Riskten Kaçınma","Riski Kabul Etme","Riski Azaltma","Riski Paylaşma" },
            ["control_types"]         = new[] { "Önleyici","Tespit Edici","Düzeltici" },
            ["control_frequencies"]   = new[] { "İşlem Başına","Günlük","Haftalık","Aylık","3 Aylık","Yıllık","Hiç" },
            ["control_effectiveness"] = new[] { "Tatmin Edici","Gelişmekte","Etkisiz","Zayıf","Hiç" },
            ["ethics_categories"]     = new[] { "Yolsuzluk / Rüşvet","Taciz / Mobbing","Ayrımcılık","Mali Usulsüzlük","Bilgi Güvenliği İhlali","Çıkar Çatışması","Diğer" },
            ["audit_categories"]      = new[] { "Mali","Operasyonel","Uyum","Bilgi Teknolojileri","İnsan Kaynakları","Satın Alma","Diğer" },
            ["audit_severities"]      = new[] { "Kritik","Yüksek","Orta","Düşük" },
            // audit_plan_types artık audit_types ile birleşti — bu key legacy uyum için korunuyor
            ["audit_plan_types"]      = new[] { "Olağan Denetim","Olağan Dışı Denetim","Takip Denetimi","Danışmanlık Görevi","İnceleme","Soruşturma" },

            // Hem plan hem iç denetim tarafında ortak kullanılan tür listesi (string[])
            ["audit_types"] = new[] { "Olağan Denetim","Olağan Dışı Denetim","Takip Denetimi","Danışmanlık Görevi","İnceleme","Soruşturma" },

            ["fk_probability"] = new ScoredOption[]
            {
                new(10.0, "Kesin Gibi", "Örn: her vardiyada defalarca tekrarlanan rutin işlem"),
                new(6.0,  "Beklenen",   "Örn: haftada birkaç kez yapılan bakım veya kontrol"),
                new(3.0,  "Mümkün",     "Örn: mevsimlik ekipman kurulumu, yıllık bakım süreçleri"),
                new(1.0,  "Nadir",      "Örn: başka sektörde yaşandı, bizde henüz olmadı"),
                new(0.5,  "Zayıf",      "Örn: birçok güvenlik katmanının aynı anda devre dışı kalması"),
                new(0.2,  "Teorik",     "Örn: laboratuvar koşullarında bile zor tekrarlanır"),
            },

            ["fk_exposure"] = new ScoredOption[]
            {
                new(10.0, "Sürekli",   "Örn: sürekli çalışan makine operatörü, kesintisiz gürültüye maruz kalanda"),
                new(6.0,  "Günlük",    "Örn: her gün kimyasal temizlik yapan temizlik personeli"),
                new(3.0,  "Haftalık",  "Örn: haftalık depo sayımında ağır kaldırma yapan çalışan"),
                new(2.0,  "Aylık",     "Örn: aylık kalibrasyon için yüksek gerilimli alana giren teknisyen"),
                new(1.0,  "Yıllık",    "Örn: yılda birkaç kez yapılan yangın tatbikatı, rutin olmayan prosedür"),
                new(0.5,  "Çok Nadir", "Örn: sistemin yılda bir kez bakım için tamamen kapatıldığı dönem"),
            },

            ["fk_consequence"] = new ScoredOption[]
            {
                new(100.0, "Felaket",        "Örn: çoklu ölüm, büyük yangın, tesisin kaybı"),
                new(40.0,  "Çok Ağır",       "Örn: tek ölüm veya kalıcı ağır sakatlık"),
                new(15.0,  "Ciddi",          "Örn: uzun süreli iş görmezlik, hastane yatışı gerektiren yaralanma"),
                new(7.0,   "Önemli",         "Örn: kırık, derin kesik gibi tıbbi müdahale gerektiren yaralanma"),
                new(3.0,   "Düşük",          "Örn: ilk yardım gerektiren hafif yaralanma, kısa süreli iş kaybı"),
                new(1.0,   "Eser Miktarda",  "Örn: çizik, morluk; ilk yardım bile gerekmeyebilir"),
            },

            // Renk kodları burada tanımlanır; RiskCalculator bu değerleri DB'den okur.
            // Hardcoded switch ifadesi kullanılmaz.
            ["fk_levels"] = new RiskLevelConfig[]
            {
                new(400.0, null,  "Çok Yüksek Risk", "#7f1d1d", "Faaliyeti durdur"),
                new(200.0, 400.0, "Yüksek Risk",     "#dc2626", "Acil önlem gerekli"),
                new(70.0,  200.0, "Önemli Risk",      "#ea580c", "Kısa sürede önlem al"),
                new(20.0,  70.0,  "Orta Risk",        "#ca8a04", "Dikkat gerekli"),
                new(0.0,   20.0,  "Kabul Edilebilir", "#16a34a", "Kabul edilebilir"),
            },

            ["site_theme"]     = new SiteThemeConfig(),
            ["app_name"]      = "RED",
            ["app_tagline"]   = "Risk · Etik · Denetim",
            ["sidebar_color"] = "#0f1f33",
            ["logo_path"]     = "",

            ["module_risk"]     = true,
            ["module_audit"]    = true,
            ["module_external"] = true,
            ["module_ethics"]   = true,

            // Fine-Kinney ilk değerlendirmede otomatik onay eşiği: skor < bu değer ise komite onayı atlanır.
            ["auto_approve_score_threshold"] = 70,

            ["review_threshold_days"] = 90,
            ["week_days"]             = 7,
            ["overdue_warn_days"]     = 7,
            ["overdue_crit_days"]     = 30,

            // Varsayılan değerler
            ["default_action_due_days"]  = 30,   // yeni aksiyon oluşturulurken önerilen vade gün sayısı
            ["default_list_page_size"]   = 25,   // liste sayfaları başına kayıt sayısı

            // Güvenlik ayarları
            ["security_password_min_length"]    = 6,
            ["security_password_complexity"]    = false,  // büyük harf + rakam zorunlu
            ["security_session_timeout_hours"]  = 8,
            ["security_reset_token_minutes"]    = 30,
            ["security_max_failed_logins"]      = 0,      // 0 = sınırsız
            ["security_lockout_minutes"]        = 15,     // hesap kilitleme süresi (dakika)

            // Aksiyon zorunluluk ayarları
            ["action_require_due_date"]     = false,
            ["action_require_responsible"]  = false,
            ["action_require_closure_note"] = false,
            ["action_require_delay_reason"] = false,

            // E-posta olay toggleları (true = gönder)
            // Risk modülü
            ["email_event_risk_proposed"]   = true,
            ["email_event_status_changed"]  = true,
            ["email_event_owner_assigned"]  = true,
            ["email_event_action_due"]      = true,
            ["action_due_remind_days"]      = 3,
            // Denetim modülü — eksik varsayılanlar eklendi
            ["email_event_finding_assigned"]    = true,
            ["email_event_closure_requested"]   = true,
            ["email_event_closure_decided"]     = true,
            ["email_event_finding_due"]         = true,
            // Etik modülü
            ["email_event_ethics_submitted"]    = true,
            ["email_event_ethics_reviewed"]     = true,

            ["email_settings"] = new EmailSettings(),

            ["risk_detail_fields"] = new Dictionary<string, bool>
            {
                ["source_type"]          = true,
                ["source"]               = true,
                ["hazard"]               = true,
                ["possible_impact"]      = true,
                ["activity_area"]        = true,
                ["affected_persons"]     = true,
                ["relevant_legislation"] = true,
            },
        };

    // ── Okuma ────────────────────────────────────────────────────────────────────

    public T Get<T>(string key)
    {
        if (_cache.TryGetValue(key, out var cached))
            return (T)cached;

        var value = LoadValue<T>(key);
        _cache.TryAdd(key, value!);
        return value;
    }

    public string[] GetList(string key) => Get<string[]>(key) ?? [];

    public bool IsModuleActive(string module) => Get<bool>($"module_{module}");

    public bool IsRiskFieldVisible(string fieldKey)
    {
        var fields = Get<Dictionary<string, bool>>("risk_detail_fields");
        if (fields is not null && fields.TryGetValue(fieldKey, out var visible))
            return visible;
        return true;
    }

    public Dictionary<string, bool> GetRiskDetailFields() =>
        Get<Dictionary<string, bool>>("risk_detail_fields")
        ?? new Dictionary<string, bool>();

    /// <summary>Fine-Kinney ilk değerlendirmede otomatik onay için skor eşiği (varsayılan: 70).</summary>
    public int GetAutoApproveScoreThreshold() =>
        Get<int>("auto_approve_score_threshold") is var t && t > 0 ? t : 70;

    public int  GetDefaultActionDueDays() => Get<int>("default_action_due_days") is var d && d > 0 ? d : 30;
    public int  GetDefaultListPageSize()  => Get<int>("default_list_page_size")  is var p && p > 0 ? p : 25;

    public int  GetPasswordMinLength()   => Get<int>("security_password_min_length")  is var n && n > 0 ? n : 6;
    public bool GetPasswordComplexity()  => Get<bool>("security_password_complexity");
    public int  GetSessionTimeoutHours() => Get<int>("security_session_timeout_hours") is var h && h > 0 ? h : 8;
    public int  GetResetTokenMinutes()   => Get<int>("security_reset_token_minutes")   is var m && m > 0 ? m : 30;
    public int  GetMaxFailedLogins()     => Get<int>("security_max_failed_logins");
    public int  GetLockoutMinutes()      => Get<int>("security_lockout_minutes") is var m && m > 0 ? m : 15;

    public string? ValidatePassword(string password)
    {
        var minLen = GetPasswordMinLength();
        if (password.Length < minLen) return $"Şifre en az {minLen} karakter olmalıdır.";
        if (GetPasswordComplexity())
        {
            if (!password.Any(char.IsUpper))  return "Şifre en az bir büyük harf içermelidir.";
            if (!password.Any(char.IsDigit))  return "Şifre en az bir rakam içermelidir.";
        }
        return null;
    }

    public bool IsActionFieldRequired(string field) => Get<bool>($"action_require_{field}");
    public bool IsEmailEventEnabled(string eventKey) => Get<bool>($"email_event_{eventKey}");
    public int  GetActionDueRemindDays()             => Get<int>("action_due_remind_days") is var d && d > 0 ? d : 3;

    public int GetWeekDays()     => Get<int>("week_days")         is var w && w > 0 ? w : 7;
    public int GetWarnDays()     => Get<int>("overdue_warn_days") is var n && n > 0 ? n : 7;
    public int GetCritDays()     => Get<int>("overdue_crit_days") is var c && c > 0 ? c : 30;

    public Email.EmailSettings GetEmailSettings() =>
        Get<Email.EmailSettings>("email_settings") ?? new Email.EmailSettings();

    public void SetEmailSettings(Email.EmailSettings settings)
    {
        Set("email_settings", settings);
        _cache.TryRemove("email_settings", out _);
    }

    public AiSettings GetAiSettings()
    {
        var s = Get<AiSettings>("ai_settings") ?? new AiSettings();
        if (string.IsNullOrEmpty(s.ApiKey)) return s;
        try { return s with { ApiKey = _protector.Unprotect(s.ApiKey) }; }
        catch { return s; /* eski şifresiz değer — olduğu gibi kullan */ }
    }

    public void SetAiSettings(AiSettings settings)
    {
        var toStore = settings with
        {
            ApiKey = string.IsNullOrEmpty(settings.ApiKey)
                ? settings.ApiKey
                : _protector.Protect(settings.ApiKey)
        };
        Set("ai_settings", toStore);
        _cache.TryRemove("ai_settings", out _);
    }

    /// <summary>Tüm konfigürasyon anahtarlarını DB'den ve varsayılanlardan birleştirerek döner.</summary>
    public Dictionary<string, object> GetAll()
    {
        var dbRows = db.AppConfigs.AsNoTracking().ToDictionary(r => r.Key, r => r.Value);
        var result = new Dictionary<string, object>(_defaults.Count);

        foreach (var (key, def) in _defaults)
        {
            if (dbRows.TryGetValue(key, out var json))
            {
                try
                {
                    result[key] = JsonSerializer.Deserialize<object>(json, _jsonOpts)!;
                    continue;
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "GetAll: '{Key}' için JSON ayrıştırma başarısız, varsayılan kullanılıyor.", key);
                }
            }
            result[key] = def;
        }
        return result;
    }

    // ── Yazma ─────────────────────────────────────────────────────────────────────

    public void Set(string key, object value)
    {
        var json = JsonSerializer.Serialize(value, _jsonOpts);
        var row  = db.AppConfigs.Find(key);
        if (row is null)
            db.AppConfigs.Add(new AppConfig { Key = key, Value = json });
        else
            row.Value = json;

        db.SaveChanges();
        _cache[key] = value;
    }

    // ── İç yardımcı ──────────────────────────────────────────────────────────────

    private T LoadValue<T>(string key)
    {
        var row = db.AppConfigs.Find(key);
        if (row is not null)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<T>(row.Value, _jsonOpts);
                if (parsed is not null) return parsed;
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "LoadValue: '{Key}' için JSON ayrıştırma başarısız, varsayılan kullanılıyor.", key);
            }
        }

        if (_defaults.TryGetValue(key, out var def))
        {
            // Varsayılan değer zaten doğru tip ise doğrudan dön (round-trip'ten kaçın).
            if (def is T typed) return typed;

            // Anonim nesne değil, DTO olduğu için bu yol nadiren tetiklenir.
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(def, _jsonOpts), _jsonOpts)!;
        }

        return default!;
    }
}
