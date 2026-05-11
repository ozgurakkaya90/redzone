namespace RiskManagement.Services.Email;

public static class EmailTemplates
{
    // $$""" ile CSS süslü parantezleri escape gerektirmez; interpolasyon {{expr}} ile yapılır
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

    public static (string Subject, string Html) RiskProposed(
        string riskCode, string riskTitle, string proposerName, string baseUrl)
    {
        var body = $"""
            <p>Sisteme yeni bir risk önerisi iletildi. İncelemenizi bekliyor.</p>
            <div class="meta">
              <b>Kod:</b> {riskCode}<br>
              <b>Başlık:</b> {riskTitle}<br>
              <b>Öneren:</b> {proposerName}
            </div>
            <a class="btn" href="{baseUrl}/risk">Riskleri İncele &rarr;</a>
            """;
        return ($"[RedZone] Yeni Risk Önerisi: {riskCode}", Wrap("Yeni Risk Önerisi", body, baseUrl));
    }

    public static (string Subject, string Html) StatusChanged(
        string riskCode, string riskTitle, string oldStatus, string newStatus, string baseUrl)
    {
        var body = $"""
            <p>Sorumlusu olduğunuz riskin durumu güncellendi.</p>
            <div class="meta">
              <b>Kod:</b> {riskCode}<br>
              <b>Risk:</b> {riskTitle}<br>
              <b>Eski Durum:</b> {oldStatus}<br>
              <b>Yeni Durum:</b> <span style="color:#1e3a5f;font-weight:700">{newStatus}</span>
            </div>
            <a class="btn" href="{baseUrl}/risk">Detaya Git &rarr;</a>
            """;
        return ($"[RedZone] Risk Durumu Güncellendi: {riskCode}", Wrap("Risk Durumu Değişti", body, baseUrl));
    }

    public static (string Subject, string Html) OwnerAssigned(
        string riskCode, string riskTitle, string baseUrl)
    {
        var body = $"""
            <p>Aşağıdaki riskin sahibi olarak atandınız. Risk yönetim sürecini takip etmeniz beklenmektedir.</p>
            <div class="meta">
              <b>Kod:</b> {riskCode}<br>
              <b>Risk:</b> {riskTitle}
            </div>
            <a class="btn" href="{baseUrl}/risk">Riske Git &rarr;</a>
            """;
        return ($"[RedZone] Risk Sahipliği Atandı: {riskCode}", Wrap("Size Bir Risk Atandı", body, baseUrl));
    }

    public static (string Subject, string Html) ActionDueSoon(
        string riskCode, string description, string dueDate, int daysLeft, string baseUrl)
    {
        var body = $"""
            <p>Sorumluluğunuzdaki bir aksiyon planının vadesi yaklaşıyor.</p>
            <div class="meta">
              <b>Risk Kodu:</b> {riskCode}<br>
              <b>Aksiyon:</b> {description}<br>
              <b>Hedef Tarih:</b> {dueDate}<br>
              <b>Kalan Süre:</b> <span style="color:#dc2626;font-weight:700">{daysLeft} gün</span>
            </div>
            <a class="btn" href="{baseUrl}/risk/actions">Aksiyonları Gör &rarr;</a>
            """;
        return ($"[RedZone] Aksiyon Vadesi Yaklaşıyor: {riskCode}", Wrap("Aksiyon Planı Hatırlatması", body, baseUrl));
    }

    public static (string Subject, string Html) Test(string baseUrl)
    {
        var body = $"""
            <p>RedZone e-posta yapılandırması başarıyla test edildi.</p>
            <div class="meta">
              <b>Sunucu:</b> SMTP bağlantısı aktif<br>
              <b>Zaman:</b> {DateTime.Now:dd.MM.yyyy HH:mm}
            </div>
            <p style="font-size:13px;color:#6b7280;margin-top:16px">
              Bu e-posta, sistem yöneticisi tarafından tetiklenmiştir.
            </p>
            """;
        return ("[RedZone] E-posta Bağlantı Testi", Wrap("Test E-postası", body, baseUrl));
    }
}
