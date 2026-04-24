using System.Text.Json;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class ConfigService(AppDbContext db)
{
    private static readonly Dictionary<string, object> _defaults = new()
    {
        ["risk_categories"]    = new[] { "Stratejik","Operasyonel","Finansal","Uyum","Bilgi Teknolojileri","İtibar","Çevre" },
        ["responsible_units"]  = new[] { "Tıbbi Koordinatörlük","İdari Koordinatörlük","Finansal Koordinatörlük","İnsan Kaynakları Direktörlüğü","Bilgi İşlem Direktörlüğü","Yönetim" },
        ["risk_strategies"]    = new[] { "Riskten Kaçınma","Riski Kabul Etme","Riski Azaltma","Riski Paylaşma" },
        ["control_types"]      = new[] { "Önleyici","Tespit Edici","Düzeltici" },
        ["control_frequencies"]= new[] { "İşlem Başına","Günlük","Haftalık","Aylık","3 Aylık","Yıllık","Hiç" },
        ["control_effectiveness"] = new[] { "Tatmin Edici","Gelişmekte","Etkisiz","Zayıf","Hiç" },
        ["ethics_categories"]  = new[] { "Yolsuzluk / Rüşvet","Taciz / Mobbing","Ayrımcılık","Mali Usulsüzlük","Bilgi Güvenliği İhlali","Çıkar Çatışması","Diğer" },
        ["audit_categories"]   = new[] { "Mali","Operasyonel","Uyum","Bilgi Teknolojileri","İnsan Kaynakları","Satın Alma","Diğer" },
        ["audit_severities"]   = new[] { "Kritik","Yüksek","Orta","Düşük" },
        ["audit_types"] = new[] {
            new { value="ordinary",label="Olağan Denetim" },
            new { value="extraordinary",label="Olağan Dışı Denetim" },
            new { value="follow_up",label="Takip Denetimi" },
            new { value="advisory",label="Danışmanlık Görevi" },
            new { value="review",label="İnceleme" },
            new { value="investigation",label="Soruşturma" }
        },
        ["fk_probability"] = new[] {
            new { value=10.0,label="10 – Çok muhtemel" },
            new { value=6.0, label="6 – Oldukça muhtemel" },
            new { value=3.0, label="3 – Olağandışı ama mümkün" },
            new { value=1.0, label="1 – Uzak ihtimal" },
            new { value=0.5, label="0.5 – Düşünülebilir ama olası değil" },
            new { value=0.2, label="0.2 – Teorik olarak mümkün" },
            new { value=0.1, label="0.1 – Pratikte mümkün değil" }
        },
        ["fk_exposure"] = new[] {
            new { value=10.0,label="10 – Sürekli" },
            new { value=6.0, label="6 – Sık sık (günlük)" },
            new { value=3.0, label="3 – Zaman zaman (haftalık)" },
            new { value=2.0, label="2 – Bazen (aylık)" },
            new { value=1.0, label="1 – Seyrek" },
            new { value=0.5, label="0.5 – Çok nadir" }
        },
        ["fk_consequence"] = new[] {
            new { value=100.0,label="100 – Çok büyük felaket" },
            new { value=40.0, label="40 – Felaket" },
            new { value=15.0, label="15 – Çok ciddi" },
            new { value=7.0,  label="7 – Ciddi" },
            new { value=3.0,  label="3 – Önemli" },
            new { value=1.0,  label="1 – Küçük" }
        },
        ["fk_levels"] = new[] {
            new { min=400.0,max=(double?)null,  label="Çok Yüksek Risk", color="#7f1d1d",action="Faaliyeti durdur" },
            new { min=200.0,max=(double?)400.0, label="Yüksek Risk",     color="#dc2626",action="Acil önlem gerekli" },
            new { min=70.0, max=(double?)200.0, label="Önemli Risk",     color="#ea580c",action="Kısa sürede önlem al" },
            new { min=20.0, max=(double?)70.0,  label="Orta Risk",       color="#ca8a04",action="Dikkat gerekli" },
            new { min=0.0,  max=(double?)20.0,  label="Kabul Edilebilir",color="#16a34a",action="Kabul edilebilir" }
        },
        ["site_theme"] = new { primary_color="#E1251B",secondary_color="#16a34a",font_family="system-ui,-apple-system,sans-serif",sidebar_color="#1a1a2e" }
    };

    public T Get<T>(string key)
    {
        var row = db.AppConfigs.Find(key);
        if (row != null)
        {
            var parsed = JsonSerializer.Deserialize<T>(row.Value);
            if (parsed != null) return parsed;
        }
        if (_defaults.TryGetValue(key, out var def))
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(def))!;
        return default!;
    }

    public string[] GetList(string key) => Get<string[]>(key) ?? [];

    public Dictionary<string, object> GetAll()
    {
        var rows = db.AppConfigs.ToDictionary(r => r.Key, r => r.Value);
        var result = new Dictionary<string, object>();
        foreach (var (key, def) in _defaults)
        {
            if (rows.TryGetValue(key, out var json))
                result[key] = JsonSerializer.Deserialize<object>(json)!;
            else
                result[key] = def;
        }
        return result;
    }

    public void Set(string key, object value)
    {
        var json = JsonSerializer.Serialize(value);
        var row = db.AppConfigs.Find(key);
        if (row == null)
            db.AppConfigs.Add(new AppConfig { Key = key, Value = json });
        else
            row.Value = json;
        db.SaveChanges();
    }

    public static string CalculateRiskLevel(double score) => score switch
    {
        >= 400 => "Çok Yüksek Risk",
        >= 200 => "Yüksek Risk",
        >= 70  => "Önemli Risk",
        >= 20  => "Orta Risk",
        _      => "Kabul Edilebilir"
    };

    public static string RiskLevelColor(string level) => level switch
    {
        "Çok Yüksek Risk" => "#7f1d1d",
        "Yüksek Risk"     => "#dc2626",
        "Önemli Risk"     => "#ea580c",
        "Orta Risk"       => "#ca8a04",
        _                 => "#16a34a"
    };
}
