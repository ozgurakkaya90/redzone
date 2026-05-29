# RED — Yedekleme ve Geri Yükleme Rehberi

> **Güncelleme yapmadan önce mutlaka yedek alın.**  
> Bu belge, veri kaybı olmadan güvenli yedekleme ve geri yükleme adımlarını açıklar.

---

## İçindekiler

1. [Neyi Yedeklemeniz Gerekiyor?](#1-neyi-yedeklemeniz-gerekiyor)
2. [Hızlı Yedek Al](#2-hızlı-yedek-al)
3. [Veritabanı Yedekleme](#3-veritabanı-yedekleme)
4. [Dosya Yedekleme](#4-dosya-yedekleme)
5. [Ayar Yedeği (Config Export)](#5-ayar-yedeği-config-export)
6. [Geri Yükleme](#6-geri-yükleme)
7. [Otomatik Yedekleme](#7-otomatik-yedekleme)
8. [Güncelleme Öncesi Kontrol Listesi](#8-güncelleme-öncesi-kontrol-listesi)

---

## 1. Neyi Yedeklemeniz Gerekiyor?

| Bileşen | Kritiklik | İçerik |
|---------|-----------|--------|
| **MySQL veritabanı** | 🔴 Kritik | Tüm riskler, denetimler, kullanıcılar, ayarlar |
| **`wwwroot/uploads/` klasörü** | 🟡 Önemli | Yüklenen dosyalar (logo, bulgular, etik ekleri) |
| **`appsettings.json`** | 🟡 Önemli | Veritabanı bağlantısı, JWT anahtarı |
| **`.env` / ortam değişkenleri** | 🟡 Önemli | Docker şifreleri |

---

## 2. Hızlı Yedek Al

Tek betikle tüm kritik verileri yedekleyin:

```bash
#!/bin/bash
# backup.sh — Çalıştırın: bash backup.sh

BACKUP_DIR="/var/backups/red/$(date +%Y%m%d_%H%M%S)"
DB_HOST="localhost"
DB_NAME="RiskManagement"
DB_USER="riskapp"
DB_PASS="sifreniz"
APP_DIR="/var/www/red"

mkdir -p "$BACKUP_DIR"

# 1. Veritabanı
echo "Veritabanı yedekleniyor..."
mysqldump -h "$DB_HOST" -u "$DB_USER" -p"$DB_PASS" \
  --single-transaction --routines --triggers \
  "$DB_NAME" | gzip > "$BACKUP_DIR/db.sql.gz"

# 2. Yüklenen dosyalar
echo "Dosyalar yedekleniyor..."
tar -czf "$BACKUP_DIR/uploads.tar.gz" -C "$APP_DIR" wwwroot/uploads/ 2>/dev/null || true

# 3. Yapılandırma
cp "$APP_DIR/appsettings.json" "$BACKUP_DIR/appsettings.json" 2>/dev/null || true

echo "Yedek tamamlandı: $BACKUP_DIR"
ls -lh "$BACKUP_DIR"
```

Docker Compose ortamında:

```bash
# Docker içindeki MySQL'den yedek al
docker exec red-db mysqldump -u riskapp -p"$DB_PASSWORD" \
  --single-transaction RiskManagement | gzip > backup_$(date +%Y%m%d).sql.gz

# Uploads klasörünü yedekle
docker cp red-app:/app/wwwroot/uploads ./backup_uploads_$(date +%Y%m%d)
```

---

## 3. Veritabanı Yedekleme

### Tam Yedek (mysqldump)

```bash
mysqldump -h localhost -u riskapp -p \
  --single-transaction \
  --routines \
  --triggers \
  --hex-blob \
  RiskManagement > red_backup_$(date +%Y%m%d).sql
```

### Sıkıştırılmış Yedek

```bash
mysqldump -h localhost -u riskapp -p RiskManagement | \
  gzip > red_backup_$(date +%Y%m%d).sql.gz
```

### Tablo Boyutlarını Kontrol Edin

```sql
SELECT table_name, 
       ROUND(data_length/1024/1024, 2) AS data_mb,
       ROUND(index_length/1024/1024, 2) AS index_mb
FROM information_schema.tables
WHERE table_schema = 'RiskManagement'
ORDER BY data_length DESC;
```

---

## 4. Dosya Yedekleme

```bash
# Tüm uploads klasörü
tar -czf uploads_backup_$(date +%Y%m%d).tar.gz wwwroot/uploads/

# Sadece bulgu ekleri
tar -czf findings_backup_$(date +%Y%m%d).tar.gz wwwroot/uploads/findings/

# Sadece etik bildirimleri ekleri
tar -czf ethics_backup_$(date +%Y%m%d).tar.gz wwwroot/uploads/ethics/
```

---

## 5. Ayar Yedeği (Config Export)

Uygulama ayarlarını JSON olarak dışa aktarın:

**Admin → Sistem Yapılandırması → Sistem Sağlığı → ↓ Ayar Yedeği**

Bu export şunları içerir:
- Tüm modül ayarları
- Tema ve logo yolu
- Risk/denetim/etik kategori listeleri
- Bildirim olayları
- Alan görünürlüğü ve aksiyon zorunlulukları

**İçermez:** SMTP şifresi, LDAP bind şifresi, JWT anahtarı, veritabanı şifresi.

> Config export, tam veritabanı yedeğinin **yerine geçmez**. Sadece ayarları taşımak veya referans için kullanılır.

---

## 6. Geri Yükleme

### Veritabanı Geri Yükleme

```bash
# Önce veritabanını oluşturun (yoksa)
mysql -u root -p -e "CREATE DATABASE IF NOT EXISTS RiskManagement CHARACTER SET utf8mb4;"
mysql -u root -p -e "GRANT ALL ON RiskManagement.* TO 'riskapp'@'%';"

# Yedeği geri yükleyin
mysql -h localhost -u riskapp -p RiskManagement < red_backup_20260101.sql

# Sıkıştırılmış yedek için:
gunzip -c red_backup_20260101.sql.gz | mysql -h localhost -u riskapp -p RiskManagement
```

### Dosyaları Geri Yükleme

```bash
# Mevcut uploads klasörünü yedekle (güvenlik önlemi)
mv wwwroot/uploads wwwroot/uploads_old

# Yedeği geri yükle
tar -xzf uploads_backup_20260101.tar.gz -C .

# İzinleri düzelt
chmod -R 755 wwwroot/uploads
```

### Migration Sonrası Kontrol

Geri yükleme tamamlandıktan sonra:

```bash
# Migration durumunu kontrol et
cd RiskManagement
dotnet ef database update

# Veya uygulama içinden:
# Admin → Sistem Yapılandırması → Veritabanı → Migration Durumu
```

---

## 7. Otomatik Yedekleme

### Cron ile Günlük Yedek

```bash
# crontab -e ile ekleyin
# Her gece 02:00'de yedek al, 30 günden eski yedekleri sil
0 2 * * * /opt/red/backup.sh >> /var/log/red-backup.log 2>&1
0 3 * * * find /var/backups/red -type d -mtime +30 -exec rm -rf {} + 2>/dev/null
```

### Docker Compose ile Otomatik Yedek

`docker-compose.yml`'e ek servis olarak eklenebilir:

```yaml
  backup:
    image: databack/mysql-backup
    environment:
      DB_SERVER: db
      DB_USER: riskapp
      DB_PASS: ${DB_PASSWORD}
      DB_NAMES: RiskManagement
      DB_DUMP_CRON: "0 2 * * *"
      DB_DUMP_TARGET: /backups
    volumes:
      - ./backups:/backups
    depends_on:
      - db
```

---

## 8. Güncelleme Öncesi Kontrol Listesi

Yeni bir sürüme geçmeden önce aşağıdaki adımları uygulayın:

- [ ] `CHANGELOG.md` dosyasındaki "Güvenlik" ve "Kırıcı Değişiklik" bölümlerini okuyun
- [ ] Veritabanı yedeği alın (`mysqldump ...`)
- [ ] `wwwroot/uploads/` klasörünü yedekleyin
- [ ] `appsettings.json` ve ortam değişkenlerini yedekleyin
- [ ] Uygulamayı durdurun (`docker compose down` veya `systemctl stop red`)
- [ ] Yeni sürümü dağıtın
- [ ] Migration'ları çalıştırın (`dotnet ef database update`)
- [ ] Uygulamayı başlatın ve giriş yapın
- [ ] Sistem Sağlığı ekranından tüm bileşenleri test edin
- [ ] Birkaç temel işlemi manuel olarak doğrulayın

Sorun çıkarsa yedeği geri yükleyip önceki sürüme dönebilirsiniz.
