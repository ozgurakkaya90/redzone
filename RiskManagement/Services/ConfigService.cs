using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services.Email;

namespace RiskManagement.Services;

public class ConfigService(AppDbContext db, ILogger<ConfigService> logger)
{
    // Scoped servis olduğu için cache per-request'tir; yine de Blazor Server'ın
    // eş zamanlı bileşen güncellemelerine karşı ConcurrentDictionary kullanılıyor.
    private readonly ConcurrentDictionary<string, object> _cache = new();

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
                new(10.0, "Kesin Gibi", "Hemen hemen her gün karşılaşılan durumlar"),
                new(6.0,  "Beklenen",   "Haftada bir veya ayda bir gerçekleşir"),
                new(3.0,  "Mümkün",     "Yılda bir veya birkaç kez gerçekleşebilir"),
                new(1.0,  "Nadir",      "Sektörde biliniyor ama bizde henüz olmadı"),
                new(0.5,  "Zayıf",      "Gerçekleşmesi için çok sıra dışı olaylar silsilesi gerekir"),
                new(0.2,  "Teorik",     "Sadece teorik olarak mümkün"),
                new(0.1,  "İmkansız",   "Pratikte gerçekleşmesi imkansız"),
            },

            ["fk_exposure"] = new ScoredOption[]
            {
                new(10.0, "Sürekli",   "Vardiya boyunca her an"),
                new(6.0,  "Günlük",    "Her gün en az bir kez"),
                new(3.0,  "Haftalık",  "Haftada bir veya birkaç kez"),
                new(2.0,  "Aylık",     "Ayda bir kez"),
                new(1.0,  "Yıllık",    "Yılda birkaç kez"),
                new(0.5,  "Çok Nadir", "Yılda birden az"),
            },

            ["fk_consequence"] = new ScoredOption[]
            {
                new(100.0, "Felaket",        "Can kaybı, kurum kapanması, lisans iptali"),
                new(40.0,  "Çok Ağır",       "Ağır yaralanma, büyük itibar ve finansal kayıp"),
                new(15.0,  "Ciddi",          "Uzun süreli iş göremezlik, operasyon durması"),
                new(7.0,   "Önemli",         "Dış müdahale gerektiren yaralanma / iş duruşu"),
                new(3.0,   "Düşük",          "Kısa süreli iş kaybı, iç müdahale yeterli"),
                new(1.0,   "Eser Miktarda",  "Görünür etki yok, sadece kayıt yeterli"),
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

            ["module_risk"]   = true,
            ["module_audit"]  = true,
            ["module_ethics"] = true,

            ["review_threshold_days"] = 90,

            ["email_settings"] = new EmailSettings(),

            ["risk_detail_fields"] = new Dictionary<string, bool>
            {
                ["source_type"]          = true,
                ["source"]               = true,
                ["hazard"]               = true,
                ["possible_impact"]      = true,
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

    public Email.EmailSettings GetEmailSettings() =>
        Get<Email.EmailSettings>("email_settings") ?? new Email.EmailSettings();

    public void SetEmailSettings(Email.EmailSettings settings)
    {
        Set("email_settings", settings);
        _cache.TryRemove("email_settings", out _);
    }

    /// <summary>Tüm konfigürasyon anahtarlarını DB'den ve varsayılanlardan birleştirerek döner.</summary>
    public Dictionary<string, object> GetAll()
    {
        var dbRows = db.AppConfigs.ToDictionary(r => r.Key, r => r.Value);
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
