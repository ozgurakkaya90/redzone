# CLAUDE.md

RED / RiskManagement — KOBİ'ler için intranet risk yönetimi, iç denetim ve etik raporlama.
ASP.NET Core 8 + **Blazor Server** + EF Core 8 + MySQL. Genel tanıtım, kurulum, roller ve
metodoloji için [README.md](README.md) ve `docs/`'a bak. Bu dosya **kodlama ajanına özel**
operasyonel bilgiyi içerir.

## Komutlar

```bash
# Build (ana proje)
cd RiskManagement && dotnet build -c Release

# Testler (xUnit + bUnit + EF InMemory) — ~403 test
dotnet test RiskManagement.Tests/RiskManagement.Tests.csproj -c Release
# Tek sınıf:  --filter "FullyQualifiedName~ExportServiceTests"

# Geliştirme (SQLite + dev)
cd RiskManagement && dotnet run
```

### Production deploy (bu kurulum: IIS in-process @ pointer.mph.com.tr)
Deploy script: **`C:\Users\ozgur.akkaya\Desktop\Deploy-RedZone.ps1`** çalıştır. Admin GEREKMEZ
(app_offline.htm tekniği). Akış: keys dizinini+izni garanti et → app_offline koy → DLL kilidi
kalkınca mevcut sürümü `C:\Publish\RedZone-Backups\<tarih>`'e yedekle (son 5) → `dotnet publish`
→ app_offline kaldır. Publish başarısızsa **yedekten otomatik geri yükler**. IIS app pool
start/stop ve `appcmd` admin ister — kullanma; yalnızca app_offline döngüsü.
- Kaynak: `C:\Publish\redzone-main\RiskManagement` · Deploy: `C:\Publish\RedZone` · App pool: `IIS APPPOOL\RedZone`
- Loglar: `C:\Publish\RedZone\logs\redzone-*.log` (Serilog, UTF-8, 14 gün). `stdoutLogEnabled=false` —
  teşhis için Serilog dosya logu veya Windows Event Log kullan.
- Prod ayarları (`ConnectionStrings`, `Jwt:Key`) yalnızca sunucu-yerel `C:\Publish\RedZone\appsettings.Production.json`'da
  (kaynak ağacında DEĞİL, ACL-kısıtlı). DB: MySQL 8, şema `RiskManagement`.

## Mimari & desenler

- **Katmanlar:** `Models/` (entity) · `Data/` (DbContext + migrations + seed) · `Services/` (iş mantığı) ·
  `Pages/` (Blazor + login Razor Pages) · `Shared/` (layout, bileşenler) · `Extensions/` (minimal-API
  endpoint'leri: export/import) · `Mcp/` (MCP araçları).
- **DbContext:** `AddDbContextFactory` + scoped fabrikadan üretilir. Her-zaman-açık/yüksek-frekanslı
  yerler (sidebar) **`IDbContextFactory` ile kısa-ömürlü context** kullanmalı — paylaşımlı scoped
  context'te "second operation on context" hatasından kaçınmak için.
- **Sorgu konvansiyonu:** Listeler `AsNoTracking` + hafif Include; çok-koleksiyonlu sorgular için
  global `QuerySplittingBehavior.SplitQuery` ayarlı (Program.cs). Sadece ID gerekirse tam entity
  yükleme — `RiskService.GetAccessibleRiskIds` / `AuditService.GetAccessibleFindingIds` gibi
  hafif `SELECT Id` metotları kullan.
- **Erişim kapsamı tek kaynakta:** `ScopeRisksForUser` / `ScopeFindingsForUser`. Yeni bir
  risk/bulgu sorgusu eklerken bu metotları yeniden kullan — kapsam kuralını elle kopyalama
  (sapma = güvenlik açığı).
- **Yetki:** `User.Role` (primary) + `UserRoles` (çoklu). `AuthService.HasPermission` izin cache'i
  version-counter ile. `admin` her şeye yetkili.
- **İş akışı:** Risk durum geçişleri tek yetkili kaynak `RiskWorkflow.CanTransition`.
- **Sayaç/kod üretimi:** `CounterHelper.GetNext` (ConcurrencyCheck + retry). `ChangeTracker.Clear()`
  çağırır — döngü içinde Add'lerle birlikte kullanma (önce topla, sonra üret/yaz; bkz. ImportService).

## DB yedekleme sistemi

- **`DbBackupService`** — mysqldump → zip → saklama. Bağlantı dizesinden host/port/user/pass regex
  ile parse eder; şifreyi **`MYSQL_PWD` ortam değişkeniyle** geçirir (komut satırı/process listesinde
  görünmez; `--no-tablespaces --single-transaction --routines --triggers`). 5 dk timeout. Başarısızsa
  `DbBackupResult(Success=false, Error=...)` döner — exception fırlatmaz.
- **`DbBackupWorker`** — her 15 dk `Tick()`, `backup_enabled` açıksa ve saati geldiyse çalışır.
  `backup_last_run` (AppConfigs, yyyyMMdd) tarih-guard'ı ile aynı gün mükerrer almaz. App pool
  recycle'dan etkilenmez. Başarısızsa o günü işaretlemez → 15 dk sonra tekrar dener.
- **Admin UI:** `/admin/db-backup` — etkinleştir, yol (yerel veya UNC ağ payı), saat, saklama sayısı,
  mysqldump yolu, "Şimdi Yedekle" butonu, son-durum tablosu, mevcut yedek listesi.
- **Varsayılan yol:** `D:\RedZone-DBBackups` (IIS_IUSRS Modify izni verildi).
  Off-box koruma için `\\nas\pay` UNC yolu girilebilir; uygulama doğrudan yazar
  (app pool kimliğinin o yola izni olmalı).
- **Standart dışı mysqldump konumu varsa** admin UI'dan yol gir; `ResolveMysqldump()` önce
  ConfigService'ten, sonra bilinen 3 varsayılan konumdan bakar.
- Özellik **opt-in** (admin UI'da açılmadan kapalıdır). İlk kullanımda "Şimdi Yedekle" ile test et.

## Tuzaklar (öğrenilmiş — tekrar etme)

- **Türkçe-I:** Makineler tr-TR kültürde. İstemci-tarafı `ToLower()` `"I"→"ı"` yapıp aramayı bozar.
  Arama/karşılaştırma terimlerinde **`ToLowerInvariant()`** kullan. Sütun-tarafı `r.X.ToLower()`
  EF tarafından SQL `LOWER()`'a çevrilir, sorun değil.
- **Excel export:** Kullanıcı metni hücreye yazılmadan önce **`ExportService.SafeCell`**'den geçmeli —
  XML-geçersiz kontrol karakterlerini temizler + formül-enjeksiyonunu (`=+-@`) tırnaklar. Yeni
  exporter eklerken unutma (RiskLibraryService kendi export'unda da kullanır).
- **E-posta şablonları:** `EmailTemplates.ApplyVars` değerleri HTML-escape eder (HTML-injection /
  phishing koruması; anonim risk önerisi kullanıcı-girdisi içerir). Türkçe'yi korumak için minimal
  escape (`& < > " '`), `WebUtility.HtmlEncode` DEĞİL.
- **Data Protection:** Anahtarlar `C:\Publish\RedZone-Keys`'e kalıcı yazılır (IIS_IUSRS Modify izni
  gerekli; deploy script garantiler). Bu olmazsa app pool recycle'da auth çerezleri/antiforgery düşer.
- **Testler:** `TestDb.Create()` **InMemory** provider (koddaki "SQLite" yorumları yanıltıcı).
  InMemory `ToLower`'ı ortam kültürüyle çalıştırır → Türkçe-I'ya duyarlı testleri
  `CultureInfo.InvariantCulture` ile sarmala (production MySQL invariant LOWER'ı yansıtsın).
- **Startup:** DB başlangıçta erişilemezse uygulama çökmez, düşük-modda başlar (geçici blip ≠ şema
  hatası). Sadece DB erişilebilir AMA migration hatalıysa prod'da fail-fast.

## Dokunma

- **Hardcoded acil admin login** (`AuthService.cs`, `username=admin` / `password=Admin123!`) — proje
  sahibi talebiyle KORUNUYOR. Düzeltme/kaldırma.

## Konvansiyonlar

- Kod yorumları ve kullanıcı-yüzü metinler **Türkçe**. Mevcut dosyanın stiline uy.
- Renkler: `wwwroot/css/tokens.css` tasarım token'ları (`--color-*`, `--chart-*`); inline stilde
  hardcoded hex yerine `var(--token)` tercih et.
- Değişiklikten sonra: `dotnet build -c Release` → ilgili testler → deploy script → site HTTP 200 doğrula.
