# MCP (Model Context Protocol) Entegrasyonu

RED, Claude ve diğer AI araçlarının MCP protokolü üzerinden risk yönetim sistemine bağlanmasına olanak tanır.

---

## API Key Oluşturma

1. **Admin → Sistem Yapılandırması → MCP / AI Bağlantı** sekmesine gidin.
2. **Yeni API Anahtarı Oluştur** butonuna tıklayın.
3. Anahtar adını girin (örn. "Claude Desktop", "CI Scripti").
4. İsteğe bağlı: **Kapsam Kullanıcı** seçin — seçilirse anahtar o kullanıcının rolü ve izinleriyle kısıtlanır.
5. Üretilen anahtarı kopyalayın — bir daha görüntülenemez.

---

## Bağlantı Yapılandırması

### Claude Code / Claude Desktop (`~/.claude.json`)

```json
{
  "mcpServers": {
    "redzone": {
      "type": "http",
      "url": "https://your-server/mcp",
      "headers": {
        "X-Api-Key": "YOUR_API_KEY"
      }
    }
  }
}
```

### Cursor / Diğer IDE

`mcp.json` veya IDE ayarlarına aynı yapıyı ekleyin.

---

## Kullanılabilir Araçlar

### `get_dashboard`
Risk yönetim sisteminin genel durumunu özetler.

**Dönüş:** Toplam risk sayısı, durum dağılımı, gecikmiş aksiyon sayısı, denetim bulgusu istatistikleri.

---

### `list_risks`
Risk envanterini filtreli olarak listeler.

| Parametre | Tür | Açıklama |
|-----------|-----|----------|
| `status` | string? | `proposed`, `under_review`, `approved`, `controlled` vb. |
| `riskLevel` | string? | Son değerlendirme seviyesi (`low`, `medium`, `high`, `critical`) |
| `category` | string? | Risk kategorisi |
| `search` | string? | Başlık veya kodda arama |
| `limit` | int | Maksimum sonuç sayısı (varsayılan: 50) |

---

### `get_risk_detail`
Belirli bir riskin tüm detaylarını döndürür.

| Parametre | Tür | Açıklama |
|-----------|-----|----------|
| `riskId` | int | Risk ID numarası |

**Dönüş:** Değerlendirmeler, kontroller, aksiyon planları, toplantı kararları.

---

### `list_action_plans`
Aksiyon planlarını filtreli olarak listeler.

| Parametre | Tür | Açıklama |
|-----------|-----|----------|
| `status` | string? | `planned`, `in_progress`, `completed`, `cancelled` |
| `overdueOnly` | bool | Sadece gecikmiş aksiyonlar |
| `riskId` | int? | Belirli bir riske ait aksiyonlar |
| `responsible` | string? | Sorumlu kişide arama |
| `limit` | int | Varsayılan: 50 |

---

### `list_findings`
İç denetim bulgularını listeler.

| Parametre | Tür | Açıklama |
|-----------|-----|----------|
| `status` | string? | `open`, `closure_requested`, `closed` |
| `severity` | string? | Ciddiyet filtresi |
| `category` | string? | Bulgu kategorisi |
| `search` | string? | Başlık veya açıklamada arama |
| `limit` | int | Varsayılan: 50 |

---

### `search`
Riskler, bulgular, dış denetimler ve etik bildirimlerinde metin araması.

| Parametre | Tür | Açıklama |
|-----------|-----|----------|
| `query` | string | Aranacak metin |
| `scope` | string | `risks`, `findings`, `external_audits`, `ethics` veya `all` |
| `limit` | int | Varsayılan: 20 |

---

### `get_risk_statistics`
Detaylı risk istatistikleri: kategori ve departman bazlı dağılım, skor analizi.

---

### `list_external_audits`
Dış denetim listesi (BRC, JCI, TSE vb.).

| Parametre | Tür | Açıklama |
|-----------|-----|----------|
| `auditingBody` | string? | Denetleyici kurum |
| `status` | string? | `planned`, `in_progress`, `completed`, `closed` |
| `fromYear` | int? | Bu yıldan itibaren filtrele |
| `limit` | int | Varsayılan: 50 |

---

### `get_external_audit_detail`
Belirli bir dış denetimin tüm detayları ve bulguları.

| Parametre | Tür | Açıklama |
|-----------|-----|----------|
| `auditId` | int | Dış denetim ID |

---

### `list_ethics_reports`
Etik bildirimlerini listeler (kişisel veri döndürülmez).

| Parametre | Tür | Açıklama |
|-----------|-----|----------|
| `status` | string? | `pending`, `audit_reviewed`, `ethics_reviewed`, `closed` |
| `category` | string? | Bildirim kategorisi |
| `limit` | int | Varsayılan: 50 |

> **Not:** Bu araç `ethics.read` iznine sahip API anahtarları için kullanılabilir.

---

### `get_ethics_summary`
Etik bildirimleri istatistikleri ve ortalama inceleme süresi. Kişisel veri içermez.

---

## Yetkilendirme

### Tam Erişim (Full Access)
Kapsam kullanıcı seçilmeden oluşturulan API anahtarları admin yetkisiyle tüm verilere erişir.

### Kısıtlı Erişim (Scoped)
Kapsam kullanıcı seçilirse API anahtarı o kullanıcının rol ve izinleriyle kısıtlanır:
- Yalnızca kullanıcının görebildiği riskler/bulgular listelenir
- Etik bildirimlere erişim `ethics.read` iznine bağlıdır

---

## Örnek Claude Code Kullanımı

```
# Gecikmiş aksiyonları listele
mcp__redzone__list_action_plans overdueOnly=true

# Belirli bir riski incele
mcp__redzone__get_risk_detail riskId=42

# "parola" içeren riskleri ara
mcp__redzone__search query="parola" scope="risks"
```
