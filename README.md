# Risk Yönetim Sistemi (RED)

KOBİ'ler için kurumsal risk yönetimi, iç denetim ve etik raporlamayı tek platformda birleştiren açık kaynaklı intranet uygulaması.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com)
[![CI](https://github.com/ozgurakkaya90/redzone/actions/workflows/ci.yml/badge.svg)](https://github.com/ozgurakkaya90/redzone/actions)

---

## Dokümantasyon

| Belge | Açıklama |
|-------|----------|
| [Kurulum Kılavuzu](docs/SETUP.md) | Ayrıntılı kurulum adımları (Docker, Manuel, Linux, Windows) |
| [Yapılandırma Rehberi](docs/CONFIGURATION.md) | SMTP, LDAP, güvenlik, modüller, dosya yükleme |
| [Yedekleme ve Geri Yükleme](docs/BACKUP_RESTORE.md) | Yedek alma, geri yükleme, güncelleme öncesi kontrol listesi |
| [Sorun Giderme](docs/TROUBLESHOOTING.md) | Yaygın sorunlar ve çözümleri |
| [Değişiklik Geçmişi](CHANGELOG.md) | Sürüm notları |

---

## Özellikler

| Modül | Açıklama |
|-------|----------|
| **Risk Yönetimi** | Fine-Kinney metodolojisi, tam iş akışı (önerildi → onaylandı → kontrol altında), kontroller ve aksiyon planları |
| **İç Denetim** | Denetim planlaması, bulgu yönetimi, kapanış talebi iş akışı, dosya ekleri |
| **Etik Raporlama** | Anonim/kimlikli bildirim, iki aşamalı inceleme (denetim + etik kurulu) |
| **Rol Tabanlı Erişim** | 9 rol, 50+ granüler izin, departman/organizasyon/şirket kapsamı |
| **LDAP/AD Entegrasyonu** | Active Directory ile otomatik kullanıcı oluşturma |
| **Dışa Aktarım** | Excel ve PDF rapor çıktısı |

---

## Hızlı Başlangıç — Docker (Önerilen)

### Ön Koşullar
- Docker Engine 24+ ve Docker Compose v2
- Minimum 1 GB RAM

### 1. Repoyu klonla

```bash
git clone https://github.com/ozgurakkaya90/redzone.git
cd risk-management-dotnet
```

### 2. Ortam değişkenlerini ayarla

```bash
# Üretim için güçlü değerler kullanın
export DB_PASSWORD="guclu-bir-sifre-secin"
export JWT_SECRET="en-az-32-karakter-uzun-rastgele-anahtar"
```

### 3. Uygulamayı başlat

```bash
docker compose up -d
```

Uygulama `http://localhost:8080` adresinde çalışmaya başlar.

**Varsayılan yönetici hesabı** (demo modda):
- Kullanıcı adı: `admin`
- Şifre: `Admin123!` *(ilk girişte değiştirin)*

---

## Manuel Kurulum (.NET CLI)

### Ön Koşullar
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- MySQL 8.0+ veya SQLite (geliştirme için)

### Adımlar

```bash
# 1. Bağımlılıkları yükle
cd RiskManagement
dotnet restore

# 2. Geliştirme ortamı için SQLite ile başlat
dotnet run
# Uygulama http://localhost:5000 adresinde başlar (SQLite kullanır)
```

### Üretim için MySQL ile kurulum

```bash
# Ortam değişkenlerini ayarla
export ConnectionStrings__DefaultConnection="Server=db-sunucusu;Database=RiskManagement;User=riskapp;Password=SIFRENIZ;"
export Jwt__Key="32-karakter-uzun-guvenli-anahtar"
export AppSettings__DemoMode=false

# Migration'ları uygula ve başlat
dotnet run --environment Production
```

---

## Konfigürasyon Referansı

| Ortam Değişkeni | Varsayılan | Açıklama |
|-----------------|-----------|----------|
| `ConnectionStrings__DefaultConnection` | — | MySQL bağlantı dizesi (production zorunlu) |
| `Jwt__Key` | — | JWT imzalama anahtarı, min 32 karakter (production zorunlu) |
| `AppSettings__DemoMode` | `false` | `true` yapıldığında örnek veri yüklenir |
| `AppSettings__UseSqlite` | `false` | `true` = SQLite kullan (sadece geliştirme için) |
| `AppSettings__SqlitePath` | `risk_management.db` | SQLite dosya yolu |
| `AppSettings__MaxUploadSizeMb` | `10` | Dosya yükleme boyut limiti (MB) |

---

## Mimari

```
RiskManagement/
├── Models/          # Domain entity'leri (Risk, AuditFinding, EthicsReport, …)
├── Data/            # EF Core DbContext + migration'lar + seed verisi
├── Services/        # İş mantığı (RiskService, AuditService, ExportService, …)
├── Pages/           # Razor Pages (login, şifre sıfırlama)
│   └── Risk/        # Risk iş akışı Blazor bileşenleri
│   └── Audit/       # İç denetim bileşenleri
│   └── Ethics/      # Etik raporlama bileşenleri
│   └── Admin/       # Yönetim paneli bileşenleri
├── Shared/          # Ortak layout ve navigasyon
└── wwwroot/         # Statik dosyalar (CSS)
```

**Teknoloji yığını:** ASP.NET Core 8 · Blazor Server · Entity Framework Core 8 · BCrypt · ClosedXML · MigraDocCore/PdfSharpCore (PDF)

Tüm bağımlılıklar permissif lisanslıdır (MIT/Apache-2.0/OFL) — tam liste için [THIRD-PARTY-LICENSES.md](THIRD-PARTY-LICENSES.md).

---

## Roller ve İzinler

| Rol | Açıklama |
|-----|----------|
| `admin` | Tam sistem erişimi |
| `committee` | Risk onay yetkisi |
| `risk_manager` | Risk kayıt yönetimi |
| `risk_owner` | Atanmış risk sahipliği |
| `auditor` | İç denetim görevi |
| `audit_manager` | Denetim planlaması |
| `ethics_board` | Etik kurul üyeliği |
| `finding_owner` | Bulgu sahipliği |
| `user` | Temel katkıcı |

Roller Admin → Roller ekranından özelleştirilebilir; her role 50+ granüler izin atanabilir.

---

## Risk Metodolojisi

Uygulama **Fine-Kinney** risk metodolojisini kullanır:

```
Risk Skoru = Olasılık (P) × Maruz Kalma (E) × Sonuç (C)
```

Eşik değerleri Admin → Konfigürasyon ekranından dinamik olarak ayarlanabilir.

---

## Testleri Çalıştırma

```bash
cd RiskManagement.Tests
dotnet test

# Kod coverage raporu ile
dotnet test --collect:"XPlat Code Coverage"

# Mutation testi (Stryker)
dotnet stryker
```

---

## Katkıda Bulunma

1. Repoyu fork'la
2. Feature branch oluştur: `git checkout -b ozellik/yeni-modul`
3. Değişikliklerini commit'le ve push'la
4. Pull Request aç

Hata bildirimi için GitHub Issues kullanın.

---

## Güvenlik

> **Sorumluluk Reddi:** Bu yazılım MIT lisansı kapsamında **"olduğu gibi" (as-is)**, açık veya
> zımni hiçbir garanti verilmeksizin sunulur. Üretim ortamında kullanmadan önce kendi güvenlik
> değerlendirmenizi yapmanız önerilir.

Üretime almadan önce **mutlaka** yapın:

- [ ] Varsayılan yönetici şifresini (`Admin123!`) ilk girişte değiştirin.
- [ ] `AppSettings__DemoMode=false` yapın (örnek veri ve demo hesapları yüklenmesin).
- [ ] `Jwt__Key` için en az 32 karakterlik **rastgele** bir değer atayın; varsayılan/şablon değer bırakmayın.
- [ ] Güçlü bir `DB_PASSWORD` belirleyin ve veritabanını ağ üzerinden dışarı açmayın.
- [ ] Uygulamayı HTTPS sonlandıran bir ters proxy (nginx / IIS / ALB) arkasında çalıştırın
      (cookie'ler üretimde `Secure` olarak işaretlenir).
- [ ] Yedekleme stratejinizi [Yedekleme Rehberi](docs/BACKUP_RESTORE.md) ile doğrulayın.

Uygulanan güvenlik önlemleri: BCrypt parola hash'leme, rol/izin tabanlı erişim, anonim uç
noktalarda IP bazlı hız sınırlama, dosya yükleme uzantı/boyut doğrulaması, güvenlik response
header'ları (CSP, X-Frame-Options vb.), antiforgery + `SameSite` çerez koruması.

### Güvenlik Açığı Bildirimi

Bir güvenlik açığı bulursanız lütfen **herkese açık issue açmayın**. Bildirim süreci için
[SECURITY.md](SECURITY.md) dosyasına bakın.

---

## Lisans

[MIT License](LICENSE) — Ticari olmayan ve ticari kullanım serbesttir.
