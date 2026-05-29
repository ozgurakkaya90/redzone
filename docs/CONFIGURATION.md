# RED — Yapılandırma Rehberi

Bu belge, uygulamanın çalışır duruma getirilmesi için gereken tüm yapılandırma seçeneklerini kapsar.

---

## İçindekiler

1. [Temel Yapılandırma](#1-temel-yapılandırma)
2. [Veritabanı](#2-veritabanı)
3. [SMTP E-posta](#3-smtp-e-posta)
4. [LDAP / Active Directory](#4-ldap--active-directory)
5. [Güvenlik Ayarları](#5-güvenlik-ayarları)
6. [Modüller](#6-modüller)
7. [Dosya Yükleme](#7-dosya-yükleme)
8. [Uygulama Görünümü](#8-uygulama-görünümü)
9. [Ortam Değişkenleri](#9-ortam-değişkenleri)
10. [Varsayılan Değerler ve Aksiyon Zorunlulukları](#10-varsayılan-değerler-ve-aksiyon-zorunlulukları)
11. [MCP ve AI (Yapay Zeka) Entegrasyonu](#11-mcp-ve-ai-yapay-zeka-entegrasyonu)

---

## 1. Temel Yapılandırma

Uygulama, iki yapılandırma katmanına sahiptir:

| Katman | Nerede? | Ne için? |
|--------|---------|----------|
| `appsettings.json` | Sunucu dosya sistemi | Veritabanı bağlantısı, JWT anahtarı |
| Veritabanı (`AppConfigs` tablosu) | Yönetici arayüzü | SMTP, tema, modüller, güvenlik politikası |

> **Not:** Veritabanı tablosundaki ayarlar `appsettings.json`'ın önüne geçer.  
> Uygulama yeniden başlatılmadan SMTP, LDAP, tema gibi ayarlar aktif olur.

---

## 2. Veritabanı

### Bağlantı Dizesi (Connection String)

`appsettings.json` veya ortam değişkeni ile ayarlanır:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=RiskManagement;User=riskapp;Password=GucluSifre;"
  }
}
```

Ortam değişkeni (Docker / Linux):
```bash
ConnectionStrings__DefaultConnection="Server=db;Database=RiskManagement;User=riskapp;Password=GucluSifre;"
```

### Migration

Migration'ları ilk kurulumda veya güncelleme sonrasında çalıştırın:

```bash
# Komut satırı
cd RiskManagement
dotnet ef database update

# Veya uygulama içi (Admin → Sistem Yapılandırması → Veritabanı → Migration Çalıştır)
```

### Önerilen MySQL Kullanıcı Yetkileri

```sql
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, DROP
  ON RiskManagement.* TO 'riskapp'@'%';
FLUSH PRIVILEGES;
```

---

## 3. SMTP E-posta

### Uygulama İçinden Ayarlama

**Admin → Sistem Yapılandırması → E-posta SMTP**

| Alan | Açıklama | Örnek |
|------|----------|-------|
| SMTP Sunucu | E-posta sunucusu adresi | `smtp.office365.com` |
| Port | SMTP portu | `587` (TLS), `465` (SSL), `25` (plain) |
| TLS/SSL | Şifreli bağlantı | Evet (önerilir) |
| Kullanıcı Adı | SMTP kimlik doğrulama | `noreply@sirket.com` |
| Şifre | SMTP şifresi | — |
| Gönderen Adresi | "From" adresi | `noreply@sirket.com` |
| Baz URL | E-posta bağlantılarındaki temel URL | `https://risk.sirket.com` |

### Hızlı Kurulum Ön Ayarları

| Sağlayıcı | Sunucu | Port | TLS |
|-----------|--------|------|-----|
| Microsoft 365 | `smtp.office365.com` | 587 | Evet |
| Gmail | `smtp.gmail.com` | 587 | Evet |
| Exchange (yerel) | `mail.sirket.local` | 25 | Hayır |
| Yandex | `smtp.yandex.com` | 587 | Evet |

### Gmail Özel Notu

Gmail için "Uygulama Şifresi" oluşturmanız gerekir (Google Hesabı → Güvenlik → 2 adımlı doğrulama açık olmalı).

### Test

"Test E-postası Gönder" butonu ile doğrulayın. Başarısızsa:
1. Güvenlik duvarı 587 portuna izin veriyor mu?
2. Kimlik bilgileri doğru mu?
3. Sunucu adresinde yazım hatası var mı?

---

## 4. LDAP / Active Directory

### Uygulama İçinden Ayarlama

**Admin → Sistem Yapılandırması → Active Directory**

| Alan | Açıklama | Örnek |
|------|----------|-------|
| LDAP Sunucu | DC adresi veya IP | `dc01.sirket.local` |
| Port | LDAP portu | `389` (LDAP), `636` (LDAPS) |
| SSL | LDAPS kullan | Önerilir (üretimde) |
| TLS | StartTLS kullan | SSL yoksa alternatif |
| Servis Hesabı DN | Dizin arama için hesap | `CN=ldapbind,OU=Servis,DC=sirket,DC=local` |
| Servis Hesabı Şifresi | Yukarıdaki hesabın şifresi | — |
| Arama Tabanı | Kullanıcıların bulunduğu OU | `OU=Kullaniciler,DC=sirket,DC=local` |
| Arama Filtresi | LDAP filtresi | `(sAMAccountName={username})` |
| Otomatik Kullanıcı Oluştur | AD kullanıcısı yoksa oluştur | Evet |
| Varsayılan Rol | Yeni oluşturulan kullanıcının rolü | `user` |

### Bağlantı Testi

"Bağlantıyı Test Et" butonu TCP + LDAP bind doğrulaması yapar.

### Kullanıcı Girişi

LDAP etkinleştirildikten sonra giriş sayfasında "Active Directory ile Giriş" seçeneği belirir. Kullanıcı adı olarak SAM account adı (`ad\kullanici` veya `kullanici`) kullanılır.

---

## 5. Güvenlik Ayarları

**Admin → Sistem Yapılandırması → Güvenlik** *(Gelişmiş mod gerektirir)*

| Ayar | Varsayılan | Açıklama |
|------|-----------|----------|
| Min. Şifre Uzunluğu | 6 | Yeni şifreler için minimum karakter sayısı |
| Şifre Karmaşıklığı | Kapalı | Büyük harf + rakam zorunluluğu |
| Oturum Süresi | 8 saat | Aktif oturumun geçerlilik süresi |
| Sıfırlama Token Süresi | 30 dakika | Şifre sıfırlama linkinin geçerlilik süresi |
| Maks. Hatalı Giriş | 0 (sınırsız) | Bu sayıya ulaşınca hesap kilitlenir. 0 = sınırsız (kilitleme yok) |
| Kilit Süresi (dk) | 15 | Hesap kilitleme süresi. Süre dolunca otomatik açılır. |

> **Öneriler:**
> - Üretim ortamı için **Maks. Hatalı Giriş = 5** veya **10** ayarlanması önerilir.
> - LDAP/AD ile giriş yapan kullanıcılar için bu limit uygulanmaz; kilitleme Active Directory tarafından yönetilir.
> - Kilitli hesap için kullanıcıya gösterilen mesaj kasıtlı olarak geneldir — hesabın gerçekten var olup olmadığını sızdırmaz.

---

## 6. Modüller

**Admin → Sistem Yapılandırması → Modüller**

Her modül bağımsız olarak açılıp kapatılabilir. Kapatılan modülün:
- Menüden kaldırılır
- URL'lerine doğrudan erişim engellenir
- Veriler **silinmez**, sadece gizlenir

| Modül | Anahtar | Varsayılan |
|-------|---------|-----------|
| Risk Yönetimi | `module_risk` | Açık |
| İç Denetim | `module_audit` | Açık |
| Dış Denetim | `module_external` | Açık |
| Etik Raporlama | `module_ethics` | Açık |

---

## 7. Dosya Yükleme

Yüklenen dosyalar iki ayrı dizinde saklanır:

| Dizin | İçerik | Notta |
|-------|--------|-------|
| `uploads/findings/` | Denetim bulgusu ekleri | İçerik kök dizini (`ContentRoot`) |
| `uploads/ethics/` | Etik bildirim ekleri | İçerik kök dizini (`ContentRoot`) |
| `wwwroot/uploads/` | Logo ve görsel varlıklar | Web kök dizini (`WebRoot`) |

Docker'da bu iki yol ayrı volume ile kalıcı hale getirilir:
- `/app/uploads` → `uploads` volume
- `/app/wwwroot/uploads` → `logo-uploads` volume

```
uploads/                       # ContentRoot — denetim ve etik ekleri
├── findings/
│   └── {findingId}/
│       └── closure/
│           └── {guid}.pdf
└── ethics/
    └── {guid}.jpg

wwwroot/uploads/               # WebRoot — logo ve görseller
└── logo.png
```

### İzinler

```bash
# Linux/macOS
chmod -R 755 uploads wwwroot/uploads
chown -R www-data:www-data uploads wwwroot/uploads  # web sunucu kullanıcısı
```

### Kapalı Modüllere URL Erişimi

Bir modül kapatıldığında:
1. Sol menüden ilgili bölüm gizlenir.
2. `/risk/*`, `/audit/*`, `/ethics/*` URL'lerine doğrudan erişimde oturum açmış kullanıcılar anasayfaya yönlendirilir.
3. Anonim sayfalar (ethics/submit, ethics/status, risk/propose) bu kuraldan muaftır.

### Desteklenen Formatlar ve Limitler

| Tür | İzin Verilen Uzantılar | Maks. Boyut |
|-----|----------------------|-------------|
| Logo | `.png`, `.jpg`, `.jpeg` | 2 MB |
| Bulgu eki | `.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.png`, `.jpg`, `.jpeg`, `.txt`, `.zip` | 10 MB |
| Etik eki | `.pdf`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.png`, `.jpg`, `.jpeg`, `.txt`, `.zip` | 10 MB |

> **Güvenlik notu:** SVG dosyaları, JavaScript içerebildiğinden (Stored XSS) güvenlik nedeniyle desteklenmemektedir. Logo için PNG veya JPG kullanın.

---

## 8. Uygulama Görünümü

**Admin → Sistem Yapılandırması → Görsel Ayarlar**

- Uygulama adı ve alt başlık
- Logo (PNG/JPG, maks. 2 MB)
- Renk paleti (birincil, ikincil, sidebar)
- Menü yazı tipi ve boyutu

Değişiklikler **anında** uygulanır, sunucu yeniden başlatması gerekmez.

---

## 9. Ortam Değişkenleri

Docker veya systemd ortamında kullanılacak değişkenler:

| Değişken | Açıklama | Örnek |
|----------|----------|-------|
| `ConnectionStrings__DefaultConnection` | MySQL bağlantı dizesi | `Server=db;...` |
| `Jwt__Key` | JWT imzalama anahtarı (min. 32 karakter) | `rastgele-32-karakter` |
| `ASPNETCORE_ENVIRONMENT` | Ortam adı | `Production` |
| `ASPNETCORE_URLS` | Dinlenecek URL | `http://+:8080` |
| `DB_PASSWORD` | Docker Compose için MySQL şifresi | `guclu-sifre` |
| `DB_ROOT_PASSWORD` | Docker MySQL root şifresi | `baska-sifre` |

> **Önemli:** `Jwt__Key` değeri üretimde en az 32 karakter olmalı ve `CHANGE_THIS` içermemelidir.  
> Bu koşul `Program.cs` tarafından denetlenir ve ihlalinde uygulama başlamaz.
