namespace RiskManagement.Services.Email;

public static class EmailTemplates
{
    // Değişken adları: {riskCode} {riskTitle} {proposerName} {oldStatus} {newStatus}
    //                  {description} {dueDate} {daysLeft} {dateTime} {baseUrl}

    private static string Wrap(string title, string body, string baseUrl) => $$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <style>
          body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Arial, sans-serif;
                 background:#f4f6f8; margin:0; padding:24px; color:#1a202c; }
          .card { background:#fff; border-radius:10px; max-width:560px; margin:0 auto;
                  box-shadow:0 1px 4px rgba(0,0,0,.08); overflow:hidden; }
          .header { background:#1e3a5f; padding:20px 28px; }
          .header h1 { margin:0; color:#fff; font-size:18px; font-weight:700; }
          .header p  { margin:4px 0 0; color:#93c5fd; font-size:13px; }
          .body   { padding:28px; }
          .body h2 { margin:0 0 12px; font-size:16px; color:#1e3a5f; }
          .meta   { background:#f8fafc; border-radius:6px; padding:14px 16px;
                    border-left:3px solid #3b82f6; margin:16px 0; font-size:13px;
                    color:#374151; line-height:1.6; }
          .btn    { display:inline-block; margin-top:18px; padding:10px 22px;
                    background:#1e3a5f; color:#fff !important; text-decoration:none;
                    border-radius:6px; font-size:13px; font-weight:600; }
          .footer { padding:16px 28px; border-top:1px solid #e5e7eb;
                    font-size:11px; color:#9ca3af; }
        </style></head>
        <body>
          <div class="card">
            <div class="header">
              <h1>&#x1F6E1; RedZone</h1>
              <p>Risk Yönetimi ve İç Denetim Sistemi</p>
            </div>
            <div class="body">
              <h2>{{title}}</h2>
              {{body}}
            </div>
            <div class="footer">
              Bu e-posta RedZone tarafından otomatik olarak gönderilmiştir.<br>
              Sorularınız için sistem yöneticinize başvurun.
            </div>
          </div>
        </body></html>
        """;

    private static string ApplyVars(string template, Dictionary<string, string> vars)
    {
        // Güvenlik: değişken DEĞERLERİ HTML-encode edilir. Gövdeler IsBodyHtml=true ile
        // gönderildiğinden, kullanıcı kontrollü serbest metin (risk başlığı, açıklama, öneren
        // adı, etik konu, bulgu başlığı) ham gömülürse bildirim e-postasına HTML/phishing
        // enjekte edilebilir — anonim risk önerisinde kimliksiz kullanıcı bile bunu yapabilir.
        // Yalnızca değerler encode edilir; şablon literal HTML'i (<div>, <b>, href) korunur.
        // Sistem ürünü kodlar/tarihler/renkler kendilerine encode olur (no-op); konu satırları
        // yalnızca kod değişkeni kullandığından bu encode'dan etkilenmez.
        foreach (var (k, v) in vars)
            template = template.Replace("{" + k + "}", EscapeHtml(v));
        return template;
    }

    /// <summary>
    /// Minimal HTML escape — yalnızca tehlikeli 5 karakteri kaçırır. WebUtility.HtmlEncode
    /// tüm ASCII-dışı karakterleri (Türkçe ö/ı/ş/İ…) sayısal entity'ye çevirip metni şişirirdi;
    /// gövde charset=utf-8 olduğundan Türkçe karakterler ham bırakılır. '&' ilk sırada olmalı.
    /// </summary>
    private static string EscapeHtml(string? s) => (s ?? "")
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#39;");

    // ── Varsayılan şablon metinleri (raw, değişken yer tutucuları içerir) ────
    public const string DefaultTpl_RiskProposed =
        "<p>Sisteme yeni bir risk önerisi iletildi. İncelemenizi bekliyor.</p>" +
        "<div class=\"meta\">" +
        "<b>Kod:</b> {riskCode}<br>" +
        "<b>Başlık:</b> {riskTitle}<br>" +
        "<b>Öneren:</b> {proposerName}" +
        "</div>" +
        "<a class=\"btn\" href=\"{baseUrl}/risk\">Riskleri İncele &rarr;</a>";

    public const string DefaultTpl_StatusChanged =
        "<p>Sorumlusu olduğunuz riskin durumu güncellendi.</p>" +
        "<div class=\"meta\">" +
        "<b>Kod:</b> {riskCode}<br>" +
        "<b>Risk:</b> {riskTitle}<br>" +
        "<b>Eski Durum:</b> {oldStatus}<br>" +
        "<b>Yeni Durum:</b> <span style=\"color:#1e3a5f;font-weight:700\">{newStatus}</span>" +
        "</div>" +
        "<a class=\"btn\" href=\"{baseUrl}/risk\">Detaya Git &rarr;</a>";

    public const string DefaultTpl_OwnerAssigned =
        "<p>Aşağıdaki riskin sahibi olarak atandınız. Risk yönetim sürecini takip etmeniz beklenmektedir.</p>" +
        "<div class=\"meta\">" +
        "<b>Kod:</b> {riskCode}<br>" +
        "<b>Risk:</b> {riskTitle}" +
        "</div>" +
        "<a class=\"btn\" href=\"{baseUrl}/risk\">Riske Git &rarr;</a>";

    public const string DefaultTpl_ActionDueSoon =
        "<p>Sorumluluğunuzdaki bir aksiyon planının vadesi yaklaşıyor.</p>" +
        "<div class=\"meta\">" +
        "<b>Risk Kodu:</b> {riskCode}<br>" +
        "<b>Aksiyon:</b> {description}<br>" +
        "<b>Hedef Tarih:</b> {dueDate}<br>" +
        "<b>Kalan Süre:</b> <span style=\"color:#dc2626;font-weight:700\">{daysLeft} gün</span>" +
        "</div>" +
        "<a class=\"btn\" href=\"{baseUrl}/risk/actions\">Aksiyonları Gör &rarr;</a>";

    // ── Denetim şablonları ───────────────────────────────────────────────────
    public const string DefaultTpl_FindingAssigned =
        "<p>Aşağıdaki denetim bulgusunun sahibi olarak atandınız. Kapatma sürecini takip etmeniz beklenmektedir.</p>" +
        "<div class=\"meta\">" +
        "<b>Kod:</b> {findingCode}<br>" +
        "<b>Bulgu:</b> {findingTitle}" +
        "</div>" +
        "<a class=\"btn\" href=\"{baseUrl}/audit/findings\">Bulgulara Git &rarr;</a>";

    public const string DefaultTpl_ClosureRequested =
        "<p>Sorumlusu olduğunuz bir bulgu için kapatma başvurusu yapıldı. İncelemenizi bekliyor.</p>" +
        "<div class=\"meta\">" +
        "<b>Kod:</b> {findingCode}<br>" +
        "<b>Bulgu:</b> {findingTitle}" +
        "</div>" +
        "<a class=\"btn\" href=\"{baseUrl}/audit/findings\">Başvuruyu İncele &rarr;</a>";

    public const string DefaultTpl_ClosureDecided =
        "<p>Kapatma başvurusu sonuçlandı.</p>" +
        "<div class=\"meta\">" +
        "<b>Kod:</b> {findingCode}<br>" +
        "<b>Bulgu:</b> {findingTitle}<br>" +
        "<b>Karar:</b> <span style=\"font-weight:700;color:{decisionColor}\">{decisionLabel}</span>" +
        "</div>" +
        "<a class=\"btn\" href=\"{baseUrl}/audit/findings\">Detaya Git &rarr;</a>";

    public const string DefaultTpl_FindingDueSoon =
        "<p>Sorumlusu olduğunuz bir denetim bulgusunun kapanma tarihi yaklaşıyor.</p>" +
        "<div class=\"meta\">" +
        "<b>Kod:</b> {findingCode}<br>" +
        "<b>Bulgu:</b> {findingTitle}<br>" +
        "<b>Hedef Tarih:</b> {dueDate}<br>" +
        "<b>Kalan Süre:</b> <span style=\"color:#dc2626;font-weight:700\">{daysLeft} gün</span>" +
        "</div>" +
        "<a class=\"btn\" href=\"{baseUrl}/audit/findings\">Bulgulara Git &rarr;</a>";

    // ── Etik şablonları ──────────────────────────────────────────────────────
    public const string DefaultTpl_EthicsSubmitted =
        "<p>Sisteme yeni bir etik bildirim iletildi. İncelemenizi bekliyor.</p>" +
        "<div class=\"meta\">" +
        "<b>Referans:</b> {ethicsCode}<br>" +
        "<b>Konu:</b> {subject}" +
        "</div>" +
        "<a class=\"btn\" href=\"{baseUrl}/ethics\">Bildirimi İncele &rarr;</a>";

    public const string DefaultTpl_EthicsReviewed =
        "<p>Bir etik bildirim incelemesi tamamlandı.</p>" +
        "<div class=\"meta\">" +
        "<b>Referans:</b> {ethicsCode}<br>" +
        "<b>Konu:</b> {subject}<br>" +
        "<b>Aşama:</b> {stage}" +
        "</div>" +
        "<a class=\"btn\" href=\"{baseUrl}/ethics\">Detaya Git &rarr;</a>";

    // ── Denetim konu satırları ────────────────────────────────────────────────
    public const string DefaultSubject_FindingAssigned  = "[RedZone] Denetim Bulgusuna Atandınız: {findingCode}";
    public const string DefaultSubject_ClosureRequested = "[RedZone] Kapatma Başvurusu: {findingCode}";
    public const string DefaultSubject_ClosureDecided   = "[RedZone] Kapatma Kararı: {findingCode}";
    public const string DefaultSubject_FindingDueSoon   = "[RedZone] Bulgu Vadesi Yaklaşıyor: {findingCode}";

    // ── Etik konu satırları ───────────────────────────────────────────────────
    public const string DefaultSubject_EthicsSubmitted  = "[RedZone] Yeni Etik Bildirim: {ethicsCode}";
    public const string DefaultSubject_EthicsReviewed   = "[RedZone] Etik Bildirim İncelendi: {ethicsCode}";

    public const string DefaultTpl_Test =
        "<p>RedZone e-posta yapılandırması başarıyla test edildi.</p>" +
        "<div class=\"meta\">" +
        "<b>Sunucu:</b> SMTP bağlantısı aktif<br>" +
        "<b>Zaman:</b> {dateTime}" +
        "</div>" +
        "<p style=\"font-size:13px;color:#6b7280;margin-top:16px\">" +
        "Bu e-posta, sistem yöneticisi tarafından tetiklenmiştir.</p>";

    // ── Varsayılan konu satırları ────────────────────────────────────────────
    public const string DefaultSubject_RiskProposed  = "[RedZone] Yeni Risk Önerisi: {riskCode}";
    public const string DefaultSubject_StatusChanged = "[RedZone] Risk Durumu Güncellendi: {riskCode}";
    public const string DefaultSubject_OwnerAssigned = "[RedZone] Risk Sahipliği Atandı: {riskCode}";
    public const string DefaultSubject_ActionDueSoon = "[RedZone] Aksiyon Vadesi Yaklaşıyor: {riskCode}";
    public const string DefaultSubject_Test          = "[RedZone] E-posta Bağlantı Testi";

    // ── Public API (customSubject/customBody boşsa varsayılan kullanılır) ────
    public static (string Subject, string Html) RiskProposed(
        string riskCode, string riskTitle, string proposerName, string baseUrl,
        string? customSubject = null, string? customBody = null)
    {
        var vars = new Dictionary<string, string>
        {
            ["riskCode"] = riskCode, ["riskTitle"] = riskTitle,
            ["proposerName"] = proposerName, ["baseUrl"] = baseUrl,
        };
        var subject = ApplyVars(
            string.IsNullOrWhiteSpace(customSubject) ? DefaultSubject_RiskProposed : customSubject, vars);
        var body = ApplyVars(
            string.IsNullOrWhiteSpace(customBody) ? DefaultTpl_RiskProposed : customBody, vars);
        return (subject, Wrap("Yeni Risk Önerisi", body, baseUrl));
    }

    public static (string Subject, string Html) StatusChanged(
        string riskCode, string riskTitle, string oldStatus, string newStatus, string baseUrl,
        string? customSubject = null, string? customBody = null)
    {
        var vars = new Dictionary<string, string>
        {
            ["riskCode"] = riskCode, ["riskTitle"] = riskTitle,
            ["oldStatus"] = oldStatus, ["newStatus"] = newStatus, ["baseUrl"] = baseUrl,
        };
        var subject = ApplyVars(
            string.IsNullOrWhiteSpace(customSubject) ? DefaultSubject_StatusChanged : customSubject, vars);
        var body = ApplyVars(
            string.IsNullOrWhiteSpace(customBody) ? DefaultTpl_StatusChanged : customBody, vars);
        return (subject, Wrap("Risk Durumu Değişti", body, baseUrl));
    }

    public static (string Subject, string Html) OwnerAssigned(
        string riskCode, string riskTitle, string baseUrl,
        string? customSubject = null, string? customBody = null)
    {
        var vars = new Dictionary<string, string>
        {
            ["riskCode"] = riskCode, ["riskTitle"] = riskTitle, ["baseUrl"] = baseUrl,
        };
        var subject = ApplyVars(
            string.IsNullOrWhiteSpace(customSubject) ? DefaultSubject_OwnerAssigned : customSubject, vars);
        var body = ApplyVars(
            string.IsNullOrWhiteSpace(customBody) ? DefaultTpl_OwnerAssigned : customBody, vars);
        return (subject, Wrap("Size Bir Risk Atandı", body, baseUrl));
    }

    public static (string Subject, string Html) ActionDueSoon(
        string riskCode, string description, string dueDate, int daysLeft, string baseUrl,
        string? customSubject = null, string? customBody = null)
    {
        var vars = new Dictionary<string, string>
        {
            ["riskCode"] = riskCode, ["description"] = description,
            ["dueDate"] = dueDate, ["daysLeft"] = daysLeft.ToString(), ["baseUrl"] = baseUrl,
        };
        var subject = ApplyVars(
            string.IsNullOrWhiteSpace(customSubject) ? DefaultSubject_ActionDueSoon : customSubject, vars);
        var body = ApplyVars(
            string.IsNullOrWhiteSpace(customBody) ? DefaultTpl_ActionDueSoon : customBody, vars);
        return (subject, Wrap("Aksiyon Planı Hatırlatması", body, baseUrl));
    }

    public static (string Subject, string Html) FindingAssigned(
        string findingCode, string findingTitle, string baseUrl,
        string? customSubject = null, string? customBody = null)
    {
        var vars = new Dictionary<string, string>
        {
            ["findingCode"] = findingCode, ["findingTitle"] = findingTitle, ["baseUrl"] = baseUrl,
        };
        var subject = ApplyVars(string.IsNullOrWhiteSpace(customSubject) ? DefaultSubject_FindingAssigned : customSubject, vars);
        var body    = ApplyVars(string.IsNullOrWhiteSpace(customBody)    ? DefaultTpl_FindingAssigned    : customBody,    vars);
        return (subject, Wrap("Denetim Bulgusuna Atandınız", body, baseUrl));
    }

    public static (string Subject, string Html) ClosureRequested(
        string findingCode, string findingTitle, string baseUrl,
        string? customSubject = null, string? customBody = null)
    {
        var vars = new Dictionary<string, string>
        {
            ["findingCode"] = findingCode, ["findingTitle"] = findingTitle, ["baseUrl"] = baseUrl,
        };
        var subject = ApplyVars(string.IsNullOrWhiteSpace(customSubject) ? DefaultSubject_ClosureRequested : customSubject, vars);
        var body    = ApplyVars(string.IsNullOrWhiteSpace(customBody)    ? DefaultTpl_ClosureRequested    : customBody,    vars);
        return (subject, Wrap("Kapatma Başvurusu", body, baseUrl));
    }

    public static (string Subject, string Html) ClosureDecided(
        string findingCode, string findingTitle, string decision, string baseUrl,
        string? customSubject = null, string? customBody = null)
    {
        var isApproved = decision == "approved";
        var vars = new Dictionary<string, string>
        {
            ["findingCode"]   = findingCode, ["findingTitle"] = findingTitle, ["baseUrl"] = baseUrl,
            ["decisionLabel"] = isApproved ? "Onaylandı" : "Reddedildi",
            ["decisionColor"] = isApproved ? "#166534"   : "#991b1b",
        };
        var subject = ApplyVars(string.IsNullOrWhiteSpace(customSubject) ? DefaultSubject_ClosureDecided : customSubject, vars);
        var body    = ApplyVars(string.IsNullOrWhiteSpace(customBody)    ? DefaultTpl_ClosureDecided    : customBody,    vars);
        return (subject, Wrap("Kapatma Kararı", body, baseUrl));
    }

    public static (string Subject, string Html) FindingDueSoon(
        string findingCode, string findingTitle, string dueDate, int daysLeft, string baseUrl,
        string? customSubject = null, string? customBody = null)
    {
        var vars = new Dictionary<string, string>
        {
            ["findingCode"] = findingCode, ["findingTitle"] = findingTitle,
            ["dueDate"] = dueDate, ["daysLeft"] = daysLeft.ToString(), ["baseUrl"] = baseUrl,
        };
        var subject = ApplyVars(string.IsNullOrWhiteSpace(customSubject) ? DefaultSubject_FindingDueSoon : customSubject, vars);
        var body    = ApplyVars(string.IsNullOrWhiteSpace(customBody)    ? DefaultTpl_FindingDueSoon    : customBody,    vars);
        return (subject, Wrap("Bulgu Vadesi Hatırlatması", body, baseUrl));
    }

    public static (string Subject, string Html) EthicsSubmitted(
        string ethicsCode, string subject, string baseUrl,
        string? customSubject = null, string? customBody = null)
    {
        var vars = new Dictionary<string, string>
        {
            ["ethicsCode"] = ethicsCode, ["subject"] = subject, ["baseUrl"] = baseUrl,
        };
        var emailSubject = ApplyVars(string.IsNullOrWhiteSpace(customSubject) ? DefaultSubject_EthicsSubmitted : customSubject, vars);
        var body         = ApplyVars(string.IsNullOrWhiteSpace(customBody)    ? DefaultTpl_EthicsSubmitted    : customBody,    vars);
        return (emailSubject, Wrap("Yeni Etik Bildirim", body, baseUrl));
    }

    public static (string Subject, string Html) EthicsReviewed(
        string ethicsCode, string subject, string stage, string baseUrl,
        string? customSubject = null, string? customBody = null)
    {
        var vars = new Dictionary<string, string>
        {
            ["ethicsCode"] = ethicsCode, ["subject"] = subject, ["stage"] = stage, ["baseUrl"] = baseUrl,
        };
        var emailSubject = ApplyVars(string.IsNullOrWhiteSpace(customSubject) ? DefaultSubject_EthicsReviewed : customSubject, vars);
        var body         = ApplyVars(string.IsNullOrWhiteSpace(customBody)    ? DefaultTpl_EthicsReviewed    : customBody,    vars);
        return (emailSubject, Wrap("Etik Bildirim İncelendi", body, baseUrl));
    }

    public static (string Subject, string Html) Test(string baseUrl,
        string? customSubject = null, string? customBody = null)
    {
        var vars = new Dictionary<string, string>
        {
            ["dateTime"] = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            ["baseUrl"]  = baseUrl,
        };
        var subject = string.IsNullOrWhiteSpace(customSubject) ? DefaultSubject_Test : customSubject;
        var body = ApplyVars(
            string.IsNullOrWhiteSpace(customBody) ? DefaultTpl_Test : customBody, vars);
        return (subject, Wrap("Test E-postası", body, baseUrl));
    }
}
