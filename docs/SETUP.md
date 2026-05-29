# RED — Risk · Etik · Denetim · Kurulum Kılavuzu

.NET 8 + Blazor Server · MySQL / SQLite · Docker

---

## İçindekiler

1. [Gereksinimler](#1-gereksinimler)
2. [Hızlı Başlangıç (Geliştirme)](#2-hızlı-başlangıç-geliştirme)
3. [Veritabanı Yapılandırması](#3-veritabanı-yapılandırması)
4. [Production Ortamı](#4-production-ortamı)
5. [Docker ile Çalıştırma](#5-docker-ile-çalıştırma)
6. [Active Directory / LDAP Entegrasyonu](#6-active-directory--ldap-entegrasyonu)
7. [Modül Yönetimi](#7-modül-yönetimi)
8. [Proje Yapısı](#8-proje-yapısı)
9. [Varsayılan Demo Kullanıcıları](#9-varsayılan-demo-kullanıcıları)

---

## 1. Gereksinimler

| Bileşen | Minimum Sürüm |
|---------|--------------|
| .NET SDK | 8.0 |
| MySQL | 8.0+ (production) |
| SQLite | yerleşik (geliştirme) |
| Docker | 24+ (opsiyonel) |

---

## 2. Hızlı Başlangıç (Geliştirme)

```bash
git clone <repo>
cd risk-management-dotnet

# SQLite + demo verilerle başlar (appsettings.Development.json)
dotnet run --project RiskManagement
# → http://localhost:5000
```

`appsettings.Development.json` varsayılan olarak şu değerleri kullanır:

```json
{
  "AppSettings": {
    "UseSqlite": true,
    "SqlitePath": "risk_management.db",
    "DemoMode": true
  }
}
```

`DemoMode: true` iken uygulama başlatıldığında `SeedData` örnek kullanıcı ve
veriler oluşturur. Gerçek veri girerken **mutlaka** `false` yapın.

---

## 3. Veritabanı Yapılandırması

### MySQL (production)

`appsettings.json` dosyasını doğrudan düzenlemeyin; connection string'i ortam
değişkeni olarak verin:

```bash
export ConnectionStrings__DefaultConnection=\
  "Server=db.sirket.com;Database=RiskManagement;User=riskapp;Password=GüçlüŞifre;SslMode=Required;"
```

Uygulama başlarken migration'ları otomatik uygular; ayrıca `dotnet ef` çalıştırmaya
gerek yoktur.

### SQLite (geliştirme / küçük kurulumlar)

```bash
export AppSettings__UseSqlite=true
export AppSettings__SqlitePath=/data/risk_management.db
```

> **Not:** SQLite, eşzamanlı yazma yükü altında performans sorunları yaşayabilir.
> 10+ aktif kullanıcılı ortamlarda MySQL tercih edin.

---

## 4. Production Ortamı

Production'da aşağıdaki ortam değişkenlerinin **tamamı** tanımlı olmalıdır:

```bash
ASPNETCORE_ENVIRONMENT=Production

# Veritabanı
ConnectionStrings__DefaultConnection="Server=...;Password=...;SslMode=Required;"

# JWT imzalama anahtarı — en az 32 rastgele karakter
Jwt__Key="buraya-en-az-32-karakter-rastgele-deger"

# Demo verilerini kapatın
AppSettings__DemoMode=false
```

`Jwt__Key` eksik ya da `CHANGE_THIS` ile başlıyorsa uygulama başlamayı reddeder.

### Parola gereksinimleri

`Jwt__Key` için güvenli rastgele değer üretmek:

```bash
openssl rand -base64 48
```

---

## 5. Docker ile Çalıştırma

```bash
docker build -t red-app .

docker run -d \
  --name red-app \
  --restart unless-stopped \
  -p 127.0.0.1:8080:8080 \
  -v red-uploads:/app/uploads \
  -v red-logo:/app/wwwroot/uploads \
  -v red-dp-keys:/root/.aspnet/DataProtection-Keys \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="Server=db;Database=RiskManagement;User=riskapp;Password=...;SslMode=Required;" \
  -e Jwt__Key="..." \
  -e AppSettings__DemoMode=false \
  red-app
```

> **Önemli:** `DataProtection-Keys` ve `uploads` volume'larını kalıcı tutun.  
> `DataProtection-Keys` olmadan her yeniden başlatmada tüm aktif oturumlar geçersiz kalır (kullanıcılar oturumunu kaybeder).  
> `uploads` olmadan yüklenen dosyalar container silindiğinde kaybolur.

---

## 6. Active Directory / LDAP Entegrasyonu

LDAP entegrasyonu **opsiyoneldir** ve varsayılan olarak kapalıdır. Açmak için iki
adım gereklidir:

1. **Yönetim → Modüller** sayfasından "Active Directory Girişi" toggle'ı açılır.
2. **Yönetim → Active Directory** sayfasından bağlantı parametreleri girilip kaydedilir.

Modül kapalıyken login ekranında AD sekmesi görünmez; tüm girişler yerel kimlik
doğrulamasıyla yapılır.

---

### 6.1 Şifreli Bağlantı — LDAPS ve StartTLS

Şifresiz LDAP (port 389) ağ üzerinde kimlik bilgilerini açık metin olarak iletir.
Production ortamında aşağıdakilerden birini kullanın:

| Seçenek | Port | Ayar |
|---------|------|------|
| **LDAPS** | 636 | "SSL Kullan (LDAPS)" işaretli, port 636 |
| **StartTLS** | 389 | "TLS Başlat (StartTLS)" işaretli, port 389 |

İkisini aynı anda işaretlemeyin; çift şifreleme bağlantı hatasına yol açar.

Sunucu sertifikası öz-imzalıysa `Novell.Directory.Ldap` kütüphanesi bağlantıyı
reddedebilir. Bu durumda:
- Sunucuya kurumsal CA sertifikası yükleyin (önerilen), ya da
- Uygulama host'una CA sertifikasını güvenilir sertifika deposuna ekleyin.

---

### 6.2 Bind Hesabı — En Az Yetki İlkesi

Uygulama, kullanıcı araması için bir "servis hesabı" (Bind DN) kullanır.
Bu hesabın aşağıdaki **minimum** yetkilerden fazlasına sahip olmaması gerekir:

- Hedef OU altında `objectClass`, `sAMAccountName`, `displayName`, `mail`, `department`
  attribute'larını **okuma** yetkisi
- Parola değiştirme veya hesap oluşturma yetkisi **gerekmez**

Active Directory'de güvenli servis hesabı oluşturma:

```
1. "OU=ServiceAccounts,DC=sirket,DC=com" altında yeni kullanıcı: svc-red-ldap
2. Hesap özelliklerinde: "Parola hiç süresi dolmasın" işaretli
3. "Kullanıcılar" OU'suna "Bu nesneyi oku" (Read) izni ver
4. Parola değiştirme ve hesap oluşturma izinlerini kaldır
5. Bind DN olarak girin:
   CN=svc-red-ldap,OU=ServiceAccounts,DC=sirket,DC=com
```

OpenLDAP için:

```ldif
dn: cn=svc-red,ou=service,dc=sirket,dc=com
objectClass: inetOrgPerson
cn: svc-red
sn: ServiceAccount
userPassword: {güçlü-şifre}
```

---

### 6.3 Search Base — Aramayı Sınırlandırın

`Arama Tabanı (Search Base)` alanına mümkün olduğunca dar bir DN verin. Kök DC'den
arama yapmak gereksiz tüm hesapları döndürür ve performansı düşürür.

```
# Çok geniş — kaçının:
DC=sirket,DC=com

# İyi — yalnızca çalışanlar:
OU=Employees,DC=sirket,DC=com

# Daha iyi — yalnızca ilgili departman:
OU=IT,OU=Employees,DC=sirket,DC=com
```

Benzer şekilde arama filtresine grup üyeliği koşulu ekleyerek erişimi
belirli bir AD grubuna kısıtlayabilirsiniz:

```
# Yalnızca "RED-Users" grubunun üyeleri:
(&(sAMAccountName={username})(memberOf=CN=RED-Users,OU=Groups,DC=sirket,DC=com))
```

---

### 6.4 Otomatik Kullanıcı Oluşturma (AutoCreate)

`AutoCreate` varsayılan olarak **kapalıdır**. Açıldığında LDAP'ta doğrulanan
ancak sistemde kaydı olmayan her kullanıcı için otomatik yerel hesap oluşturulur.

**Güvenlik değerlendirmesi:**

| Durum | Öneri |
|-------|-------|
| Tüm AD kullanıcıları sisteme girebilmeli | AutoCreate açık, DefaultRole = `user` |
| Yalnızca belirli kişiler erişebilmeli | AutoCreate kapalı; kullanıcıları manuel ekleyin |
| Rol kısıtlaması önemliyse | AutoCreate kapalı; girişte manuel rol atayın |

`DefaultRole` için **`admin` seçmeyin.** AD hesabı ele geçirildiğinde sistem
tamamen açık hale gelir. En kısıtlı rol (`user`) ile başlayın, yetkiyi sonradan
yükseltin.

Otomatik oluşturulan hesaplarda `PasswordHash = "$ldap$"` değeri saklanır; bu
değerle yerel parola girişi yapılamaz — hesap yalnızca AD üzerinden erişilebilir.

---

### 6.5 Hata Ayıklama

| Belirti | Olası neden |
|---------|-------------|
| "LDAP sunucusuna ulaşılamıyor" | Firewall port 389/636'yı kapatmış |
| "Kimlik doğrulama başarısız" | Yanlış şifre **veya** hesap kilitli |
| "Kullanıcı bulunamadı" | Search Base yanlış ya da filtre eşleşmiyor |
| Sertifika hatası | LDAPS aktif, CA güvenilir depoya eklenmemiş |
| "Sistemde tanımlı değil" | AutoCreate kapalı, kullanıcı manuel eklenmemiş |

Geliştirme ortamında detaylı log için:

```json
"Logging": {
  "LogLevel": {
    "Default": "Debug"
  }
}
```

---

## 7. Modül Yönetimi

**Yönetim → Modüller** sayfasından aşağıdaki bileşenler bağımsız açılıp kapatılabilir:

| Modül | Kapatıldığında |
|-------|---------------|
| Risk Yönetimi | Risk envanteri ve ilgili menüler gizlenir |
| İç Denetim | Denetim ve bulgu sayfaları gizlenir |
| Etik Yönetimi | Etik bildirim sayfaları ve login ekranındaki anonim bildirim butonu gizlenir |
| Active Directory Girişi | Login ekranındaki AD sekmesi kaldırılır |

Modül kapatılırken veriler **silinmez**; yalnızca arayüzden gizlenir.

---

## 8. Proje Yapısı

```
RiskManagement/
├── Models/
│   ├── Config.cs          → AppConfig, LdapConfiguration, ...
│   ├── ConfigDtos.cs      → RiskLevelConfig, ScoredOption, SiteThemeConfig, ...
│   ├── Risk.cs            → Risk, Evaluation, Control, ActionPlan, ...
│   ├── Audit.cs           → InternalAudit, AuditFinding, ClosureRequest, ...
│   ├── Ethics.cs          → EthicsReport, EthicsAttachment
│   └── User.cs            → User, UserRole, Department, Organization, ...
├── Data/
│   ├── AppDbContext.cs    → EF Core context, ilişki konfigürasyonları
│   └── SeedData.cs        → Demo veriler (DemoMode=true iken çalışır)
├── Services/
│   ├── AuthService.cs     → Yerel kimlik doğrulama, rol/yetki, BuildPrincipal
│   ├── ConfigService.cs   → ConcurrentDictionary cache, AppConfig CRUD
│   ├── IRiskCalculator.cs → Fine-Kinney hesaplama arayüzü
│   ├── RiskCalculator.cs  → Skor → seviye, seviye → renk (DB'den dinamik)
│   ├── RiskService.cs     → Risk iş mantığı, Counter-tabanlı kod üretimi
│   ├── AuditService.cs    → Denetim iş mantığı, dosya yükleme sabitleri
│   ├── EthicsService.cs   → Etik bildirim iş mantığı
│   └── TaskService.cs     → Bekleyen görevler
├── Pages/
│   ├── Login.cshtml(.cs)  → Razor Page — AD/yerel giriş, koşullu render
│   ├── Risk/              → Risk envanteri, detay, kontrol matrisi, ...
│   ├── Audit/             → Denetim paneli, bulgular, kapanış akışı
│   ├── Ethics/            → Bildirim formu, yönetim, anonim sorgulama
│   └── Admin/             → Kullanıcılar, modüller, LDAP, yapılandırma
├── Shared/
│   ├── MainLayout.razor   → Sidebar, modül görünürlüğü, multi-role badge
│   └── Components/        → EvalBox, FineKinneyForm, ToastContainer
├── Properties/
│   └── launchSettings.json → Geliştirme profili (ASPNETCORE_ENVIRONMENT=Development)
├── appsettings.json           → Production şablonu (secret içermez)
└── appsettings.Development.json → Dev overrides (SQLite, DemoMode)
```

---

## 9. Varsayılan Demo Kullanıcıları

`DemoMode: true` iken oluşturulan hesaplar. **Production'da kullanmayın.**

| Kullanıcı adı | Şifre | Rol |
|---------------|-------|-----|
| `admin` | `Admin123!` | Yönetici |
| `demo` | `Demo123!` | Yönetici (demo) |
| `t.yilmaz` | `Demo123!` | Risk Komitesi |
| `f.arslan` | `Demo123!` | Risk Yöneticisi |
| `m.yilmaz` | `Demo123!` | Risk Sahibi |
| `c.sahin` | `Demo123!` | Denetim Müdürü |
| `e.kurt` | `Demo123!` | Denetçi |
| `s.yildiz` | `Demo123!` | Etik Kurul |

> Tüm demo hesapların şifresi `Demo123!`, yalnızca `admin` hesabı `Admin123!` kullanır.
