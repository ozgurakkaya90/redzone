# Değişiklik Geçmişi (CHANGELOG)

Bu dosya [Anlamlı Sürümleme](https://semver.org/lang/tr/) (SemVer) kurallarına göre tutulmaktadır.

---

## [Yayımlanmamış]

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
