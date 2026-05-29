# RED — Sorun Giderme Rehberi

Bu belge, kurulum ve kullanım sırasında karşılaşılan yaygın sorunlar ile çözümlerini kapsar.

---

## İçindekiler

1. [Uygulama Başlamıyor](#1-uygulama-başlamıyor)
2. [Veritabanı Sorunları](#2-veritabanı-sorunları)
3. [Giriş Sorunları](#3-giriş-sorunları)
4. [LDAP / Active Directory Sorunları](#4-ldap--active-directory-sorunları)
5. [E-posta Gönderilmiyor](#5-e-posta-gönderilmiyor)
6. [Dosya Yükleme Sorunları](#6-dosya-yükleme-sorunları)
7. [Performans Sorunları](#7-performans-sorunları)
8. [Migration Sorunları](#8-migration-sorunları)
9. [Docker Sorunları](#9-docker-sorunları)
10. [Destek Paketi İndirme](#10-destek-paketi-i̇ndirme)

---

## 1. Uygulama Başlamıyor

### Hata: "JWT key must be at least 32 characters"

```
System.InvalidOperationException: Üretim ortamında JWT anahtarı geçersiz veya değiştirilmemiş.
```

**Çözüm:** `Jwt__Key` ortam değişkenini veya `appsettings.json`'daki `Key` alanını en az 32 karakter güçlü bir değerle güncelleyin:

```bash
export Jwt__Key="rastgele-ve-guclu-jwt-anahtari-buraya"
```

---

### Hata: "Connection string contains CHANGE_ME"

**Çözüm:** `DefaultConnection` bağlantı dizesini gerçek MySQL kimlik bilgileriyle güncelleyin.

---

### Hata: Port 8080 zaten kullanımda

```bash
# Hangi işlem kullanıyor?
lsof -i :8080

# Docker Compose'da portu değiştirin
# docker-compose.yml → ports: "8081:8080"
```

---

### Uygulama hata sayfası gösteriyor (500)

1. Uygulama loglarını kontrol edin:
   ```bash
   docker logs red-app --tail 50
   # veya
   journalctl -u red -n 50
   ```
2. `ASPNETCORE_ENVIRONMENT=Development` ayarlayarak daha ayrıntılı hata mesajı alın (üretimde değil!)

---

## 2. Veritabanı Sorunları

### Veritabanına bağlanamıyor

```
MySqlException: Unable to connect to any of the specified MySQL hosts
```

**Kontrol listesi:**
- [ ] MySQL servisi çalışıyor mu? `systemctl status mysql` veya `docker ps`
- [ ] Sunucu adresi doğru mu? (`Server=localhost` yerine Docker ağında `Server=db`)
- [ ] Port doğru mu? (3306)
- [ ] Kullanıcı adı ve şifre doğru mu?
- [ ] MySQL kullanıcısı bu makineye/IP'ye bağlanma iznine sahip mi?

```sql
-- MySQL'de bağlantı yetkisini kontrol et
SELECT user, host FROM mysql.user WHERE user = 'riskapp';
```

---

### Erişim engeli (Access denied)

```sql
-- Yetki ver
GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, INDEX, DROP
  ON RiskManagement.* TO 'riskapp'@'%';
FLUSH PRIVILEGES;
```

---

### Sistem Sağlığı ekranından test

**Admin → Sistem Yapılandırması → Sistem Sağlığı → Veritabanı → Test Et**

---

## 3. Giriş Sorunları

### "Kullanıcı adı veya şifre hatalı" — Şifre doğru

1. Kullanıcı aktif mi? (`Admin → Kullanıcılar → Kullanıcı adı → Aktif` kutusunu kontrol edin)
2. BCrypt hash bozulmuş olabilir — admin kullanıcı şifresini sıfırlayın:
   ```bash
   # Doğrudan DB'den şifre sıfırla (geçici, sonra uygulama içinden değiştirin)
   dotnet run -- reset-password admin yeni-sifre  # eğer CLI desteği varsa
   ```

---

### Hesap kilitli

```
Hesabınız çok fazla hatalı girişten dolayı kilitlendi.
```

**Çözüm:**
- 15 dakika bekleyin (otomatik açılır)
- Veya admin `LockoutUntil` alanını NULL yapabilir:
  ```sql
  UPDATE Users SET LockoutUntil = NULL, FailedLoginCount = 0 WHERE Username = 'kullaniciadi';
  ```
- Maks. hatalı giriş limitini 0 yaparak kilitlemeyi tamamen devre dışı bırakabilirsiniz:  
  **Admin → Sistem Yapılandırması → Güvenlik → Maks. Hatalı Giriş: 0**

---

### Admin şifresini unuttu, başka admin yok

```sql
-- Yeni bir şifre hash'i oluşturun (.NET BCrypt)
-- Aşağıdaki değer "Admin1234!" için örnek hash'tir — kendi şifreniz için uygulama üzerinden oluşturun
UPDATE Users 
SET PasswordHash = '$2a$11$...' 
WHERE Username = 'admin' AND AuthType = 'local';
```

Alternatif: Uygulamayı geliştirme modunda çalıştırıp seed verisini kullanabilirsiniz.

---

## 4. LDAP / Active Directory Sorunları

### Bağlantı zaman aşımı

```
[HATA] TCP: dc01.sirket.local:389 erişilemiyor.
```

**Kontrol listesi:**
- [ ] Güvenlik duvarı 389 (LDAP) veya 636 (LDAPS) portuna izin veriyor mu?
- [ ] DC sunucusunun IP'si ve adı doğru mu?
- [ ] `ping dc01.sirket.local` çalışıyor mu?

---

### Bind başarısız

```
[HATA] Invalid credentials
```

- Servis hesabı şifresi değişmiş olabilir
- DN formatı yanlış olabilir: `CN=ldapbind,OU=Servis,DC=sirket,DC=local`

---

### Kullanıcı bulunamadı

```
Active Directory'de kullanıcı bulunamadı.
```

- Arama tabanı (`UserSearchBase`) doğru OU'yu işaret ediyor mu?
- Arama filtresi (`sAMAccountName={username}`) doğru mu?
- Servis hesabının arama OU'sunda okuma yetkisi var mı?

---

### LDAP test aracı

**Admin → Sistem Yapılandırması → Active Directory → Bağlantıyı Test Et**

Adım adım TCP, LDAP bağlantı ve bind testlerini gösterir.

---

## 5. E-posta Gönderilmiyor

### SMTP testi başarısız

**Admin → Sistem Yapılandırması → Sistem Sağlığı → E-posta → Test Et**

Yaygın hatalar:

| Hata | Çözüm |
|------|-------|
| `Connection refused` | Port yanlış veya güvenlik duvarı kapalı |
| `Authentication failed` | Kullanıcı adı/şifre hatalı |
| `SSL handshake failed` | TLS/SSL ayarı yanlış |
| `Relay access denied` | Sunucu bu IP'den gönderime izin vermiyor |

---

### E-postalar kuyruğa alınıyor ama gönderilmiyor

1. `EmailWorker` arka plan servisi çalışıyor mu? (uygulama yeniden başlatın)
2. SMTP ayarları değiştirilmişse: uygulama yeniden başlatılmadan otomatik yüklenir, birkaç dakika bekleyin

---

### Gmail / Microsoft 365 özel notlar

**Gmail:** Hesap güvenliği → "Uygulama şifresi" oluşturun. Normal şifre çalışmaz.  
**Microsoft 365:** Modern kimlik doğrulama gerekebilir; SMTP AUTH'un tenant düzeyinde etkin olduğunu kontrol edin.

---

## 6. Dosya Yükleme Sorunları

### "Klasör bulunamadı veya yazılamıyor"

```bash
# Klasör oluştur ve izin ver (denetim/etik ekleri ContentRoot altında)
mkdir -p uploads/findings
mkdir -p uploads/ethics
chmod -R 755 uploads
chown -R www-data:www-data uploads

# Logo WebRoot altında
mkdir -p wwwroot/uploads
chmod -R 755 wwwroot/uploads
chown -R www-data:www-data wwwroot/uploads

# Docker'da
docker exec red-app mkdir -p /app/uploads/findings /app/uploads/ethics /app/wwwroot/uploads
docker exec red-app chmod -R 755 /app/uploads /app/wwwroot/uploads
```

---

### "SVG güvenlik nedeniyle desteklenmez"

Logo için PNG veya JPG kullanın. SVG dosyaları güvenlik nedeniyle kabul edilmemektedir.

---

### Dosya boyutu hatası

| Yükleme türü | Maks. boyut |
|-------------|-------------|
| Logo | 2 MB |
| Bulgu eki | 10 MB |
| Etik bildirimi eki | 10 MB |

Dosyayı sıkıştırın veya bölerek yükleyin.

---

## 7. Performans Sorunları

### Sayfa yavaş açılıyor

1. Sistem Sağlığı → Veritabanı testini çalıştırın
2. Tablolarda eksik index olabilir — `database-schema.sql` dosyasındaki indexleri kontrol edin
3. Büyük import dosyaları arka planda işlenir; birkaç dakika bekleyin

---

### Bekleyen İşler sayfası yavaş

`TaskService` her sayfa açılışında birden fazla DB sorgusu çalıştırır. Optimizasyon:
- Risk ve denetim sayısı binleri aşıyorsa status bazlı indexler ekleyin:
  ```sql
  CREATE INDEX idx_risks_status ON Risks(Status);
  CREATE INDEX idx_audit_findings_status ON AuditFindings(Status);
  ```

---

## 8. Migration Sorunları

### Migration çalıştırılınca hata

```
Table 'X' already exists
```

**Çözüm:** Migration geçmişi bozulmuş olabilir. `__EFMigrationsHistory` tablosunu kontrol edin:

```sql
SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId;
```

---

### "There are pending migrations"

Uygulama başlarken bu mesajı görüyorsanız:

```bash
cd RiskManagement
dotnet ef database update
```

Veya **Admin → Sistem Yapılandırması → Veritabanı → Migration Çalıştır** (onay gerektirir).

---

## 9. Docker Sorunları

### Container başlamıyor

```bash
# Log'ları görüntüle
docker compose logs app --tail 50

# Container durumunu kontrol et
docker compose ps
```

---

### "db: connection refused" — DB container hazır değil

MySQL başlamadan uygulama başlıyorsa:

```bash
docker compose restart app  # DB hazır olduktan sonra app'i yeniden başlat
```

Veya `docker-compose.yml`'e sağlık denetimi ekleyin (mevcut dosyada healthcheck var).

---

### Veriler kayboldu

Docker volume'ları kontrol edin:

```bash
docker volume ls | grep red
docker volume inspect red_db-data
```

Volume varsa veri kaybolmamıştır. Container'ı yeniden oluşturmak veriyi silmez.

---

## 10. Destek Paketi İndirme

Sorun raporlarken destek paketini ekleyin:

**Admin → Sistem Yapılandırması → Sistem Sağlığı → ↓ Destek Paketi**

Paket şunları içerir (hassas bilgiler maskelenir):
- Uygulama versiyonu ve .NET runtime
- İşletim sistemi
- Aktif modüller
- Migration durumu
- Veritabanı / SMTP / klasör test sonuçları
- Son ayar değişiklikleri

> **Güvenlik:** Destek paketi şifre, token veya connection string içermez.

---

## Hâlâ Sorun Var mı?

1. [GitHub Issues](https://github.com/ozgurakkaya90/redzone/issues) sayfasına sorun bildirin
2. Destek paketini (`support-package-*.json`) ve hata logunu ekleyin
3. Hangi adımları denediğinizi belirtin
