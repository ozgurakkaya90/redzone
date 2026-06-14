# Değişiklik Geçmişi (CHANGELOG)

Bu dosya [Anlamlı Sürümleme](https://semver.org/lang/tr/) (SemVer) kurallarına göre tutulmaktadır.

---

## [1.1.0] — 2026-06-14

### Önemli — PDF kütüphanesi & lisans temizliği
- PDF üretimi QuestPDF'ten (Community lisans kısıtı) tamamen permissif **MigraDocCore/PdfSharpCore** (MIT) + gömülü Lato (OFL) font resolver'a taşındı. Artık tüm bağımlılıklar ticari kullanıma sınırsız serbest — bkz. `THIRD-PARTY-LICENSES.md`.

### Eklendi
- **KVKK/GDPR "unutulma hakkı"**: kullanıcı yönetiminde anonimleştirme eylemi (iki adımlı onay, son-admin koruması)
- Kalıntı risk kabulünde gerekçe zorunluluğu + karar sahibi/tarih + yeniden gözden geçirme tarihi
- Fine-Kinney P/E/C değerleri için sunucu-tarafı skala doğrulaması (MCP/import/API savunması)
- OSS dosyaları: `CODE_OF_CONDUCT.md`, `ROADMAP.md`, `THIRD-PARTY-LICENSES.md`, issue/PR şablonları, Dependabot

### Güvenlik
- Kalıntı risk kabulü artık `risk.manage` yetkisi zorunlu kılıyor (yetki-atlama kapatıldı)
- Tüm risk durum geçişleri tek kapıdan (`RiskWorkflow.CanTransition`) doğrulanıyor
- Güvenlik testleri: MCP kapsam izolasyonu, import yetki-sınırı, MCP API anahtarı doğrulaması

### Düzeltildi
- Özet rapordaki ölü "Tamamlandı" KPI'ı (hiç var olmayan durumu sayıyordu) gerçek metrikle değiştirildi
- MCP etik özeti yanlış durum sabitiyle "kurul incelemesi bekleyen" sayısı üretiyordu
- Risk import'unda sessiz veri kaybı (CounterHelper `ChangeTracker.Clear()` döngü-içi) giderildi
- Kabul gerekçesi denetim iznine yanlış alanla (`RejectionReason`) yazılıyordu

### İç temizlik
- 14 çağrılmayan metot + kullanılmayan `UpdateCheckService` kaldırıldı
- Form etiketleri ekran okuyucuya programatik bağlandı; emoji ikonlar tutarlı SVG'ye taşındı
- EF design-time snapshot'ı MySQL sağlayıcısına hizalandı (migration kirliliği giderildi)

### Güvenlik — Sağlamlaştırma Turu 2
- Hesap kilitleme mesajı artık kullanıcı varlığını sızdırmıyor; generic mesaj gösteriliyor
- Kilit süresi sabit 15 dakikadan config'e taşındı (`security_lockout_minutes`)
- Modül guard middleware anonim sayfaları (`/ethics/submit`, `/ethics/status`, `/risk/propose`) ve Blazor altyapı yollarını (`/_blazor`, `/_framework`, `/_content`) doğru şekilde muaf tutuyor

### İyileştirme — Sağlamlaştırma Turu 2
- `RecentLogBuffer`: son 50 Warning/Error log girişini bellekte tutan ring buffer eklendi; Sistem Sağlığı ekranında görüntüleniyor
- `TaskService`: kullanıcı bazlı 30 saniyelik in-memory cache eklendi (`IMemoryCache`, farklı kullanıcılar arasında izolasyon sağlandı)
- Config import: JSON dosyası yükleyip önizleme + onay akışıyla güvenli içe aktarma eklendi; hassas alanlar import edilmiyor
- `RiskHistoryCard.razor`: RiskDetail.razor'dan Tarihçe kartı bağımsız salt-okunur componente çıkarıldı
- Güvenlik ayarları ekranına Maks. Hatalı Giriş ve Kilit Süresi alanları eklendi
- `.gitignore` genişletildi: `publish/`, `.claude/`, `.env`, `*.pfx`, `docker-compose.override.yml`

### Güvenlik
- SVG logo yükleme desteği kaldırıldı — `<script>` içerebilen SVG dosyaları XSS riski taşıdığı için artık yalnızca PNG ve JPG kabul edilmektedir
- Hesap kilitleme mekanizması eklendi: Sistem Yapılandırması → Güvenlik ekranından maksimum hatalı giriş sayısı ayarlanabilir; eşik aşıldığında hesap 15 dakika kilitlenir
- Kapalı modüllerin URL'lerine doğrudan erişim middleware ile engellendi

### İyileştirme
- `TaskService.GetTasksForUser()` sorgu optimizasyonu: Önceki sürümde admin/committee rolü için 4+3+2 ayrı veritabanı sorgusu yapılırken şimdi 3 toplu (batch) sorguya indirildi
- Sistem Yapılandırması: ayar arama kutusu, basit/gelişmiş mod, onay modalları, ayar değişiklik geçmişi, config export ve destek paketi eklendi
- Sağlık ekranına "ne yapmalıyım" önerileri ve ilgili sekmeye navigate bağlantıları eklendi
- Kurulum kontrol listesi eklendi

### Dokümantasyon
- `docs/CONFIGURATION.md` — SMTP, LDAP, güvenlik ayarları rehberi
- `docs/BACKUP_RESTORE.md` — yedekleme ve geri yükleme stratejisi
- `docs/TROUBLESHOOTING.md` — sık karşılaşılan sorunlar
- `CHANGELOG.md` — bu dosya

---

## [1.0.0] — 2026-05-01

### İlk Yayım
- Risk Yönetimi modülü: Fine-Kinney metodolojisi, tam iş akışı (önerildi → onaylandı → kontrol altında → kapandı), kontroller ve aksiyon planları
- İç Denetim modülü: denetim planlaması, bulgu yönetimi, kapanış talebi iş akışı, dosya ekleri, etkinlik kaydı
- Dış Denetim modülü: dış kurum tanımlama, denetim kaydı, yetki yönetimi
- Etik Raporlama modülü: anonim/kimlikli bildirim, iki aşamalı inceleme
- Rol tabanlı erişim: 9 rol, 50+ granüler izin
- LDAP/Active Directory entegrasyonu
- Excel ve PDF dışa aktarım
- SMTP e-posta bildirimleri
- Docker Compose ile tek komut kurulum
- Sistem Yapılandırması: SMTP, LDAP, tema, logo, modül yönetimi

---

## Güncelleme Notu

Yeni sürüme geçmeden önce:
1. Veritabanınızı yedekleyin: `docs/BACKUP_RESTORE.md` → "Hızlı Yedek Al" bölümü
2. `CHANGELOG.md` dosyasındaki "Güvenlik" ve "Kırıcı Değişiklik" başlıklarını okuyun
3. Migration'ları uygulayın: `dotnet ef database update` veya uygulama içi migration arayüzü
