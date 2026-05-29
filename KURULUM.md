# RedZone — İntranet Kurulum Rehberi

Bu rehber, RedZone'u kurumunuzun ağında Windows Server veya Linux üzerinde çalıştırmak için hazırlanmıştır. Komut satırı deneyimi gerektirmez.

---

## Hangi Yolu Seçeyim?

| Durum | Önerilen Yol |
|-------|--------------|
| Windows Server (2019/2022) var | → **Yol A: PowerShell Kurulumu** |
| Docker kullanıyoruz | → **Yol B: Docker Compose** |
| Linux sunucu var | → **Yol C: Linux Servis** |
| Sadece denemek istiyorum | → **Yol D: Hızlı Test** |

---

## Yol A — Windows Server (Önerilen)

### Ön Koşullar

1. **Windows Server 2019 veya 2022**
2. **Yönetici (Administrator) yetkisi**
3. **.NET 8 Runtime** — yoksa kurulum scripti sizi yönlendirir

### Kurulum (3 adım)

**1. Adım — Dosyaları sunucuya kopyalayın**

RedZone klasörünü (bu repoyu) sunucuya kopyalayın.  
Örneğin: `C:\Kurulum\RedZone`

**2. Adım — PowerShell'i Yönetici olarak açın**

Başlat menüsüne sağ tıklayın → "Windows PowerShell (Yönetici)"

**3. Adım — Kurulum scriptini çalıştırın**

```powershell
cd C:\Kurulum\RedZone
.\install-windows.ps1
```

Script şunları otomatik yapar:
- ✅ .NET 8 Runtime kontrol eder, yoksa indirme sayfasını açar
- ✅ Uygulamayı `C:\Program Files\RedZone` klasörüne kurar
- ✅ Veritabanını `C:\ProgramData\RedZone\redzone.db` oluşturur
- ✅ Windows Service olarak kaydeder (sunucu yeniden başladığında otomatik çalışır)
- ✅ Güvenlik duvarında TCP 5000 portunu açar
- ✅ Sağlık kontrolü yaparak kurulumu doğrular

**Varsayılan port 5000 yerine başka port kullanmak için:**
```powershell
.\install-windows.ps1 -Port 8080
```

### Kurulumdan Sonra

Tarayıcıdan `http://SUNUCU_ADI:5000` adresine gidin.

```
Varsayılan yönetici girişi:
  Kullanıcı adı : admin
  Şifre         : Admin123!
  
⚠ İlk girişte şifrenizi mutlaka değiştirin.
```

### Servis Yönetimi

```powershell
# Servisi durdur
Stop-Service RedZone

# Servisi başlat
Start-Service RedZone

# Servis durumunu gör
Get-Service RedZone

# Kaldır
.\uninstall-windows.ps1
```

### SSL (HTTPS) Kurulumu

İntranet ortamında genellikle kurumsal CA sertifikası kullanılır.  
En basit yol: IIS veya Nginx'i **ters proxy** olarak önüne koymak.

**IIS ile HTTPS (kısa yol):**
1. IIS Manager → Sites → Add Website
2. Physical Path: `C:\Program Files\RedZone`
3. Binding: HTTPS, port 443, sertifikanızı seçin
4. Aşağıdaki `web.config` dosyasını `C:\Program Files\RedZone\` içine oluşturun:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <handlers>
      <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
    </handlers>
    <aspNetCore processPath="dotnet" arguments=".\RiskManagement.dll" stdoutLogEnabled="false" hostingModel="inprocess">
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
      </environmentVariables>
    </aspNetCore>
  </system.webServer>
</configuration>
```

**Nginx ile HTTPS:**
```nginx
server {
    listen 443 ssl;
    server_name redzone.sirketiniz.local;

    ssl_certificate     /etc/ssl/sirket.crt;
    ssl_certificate_key /etc/ssl/sirket.key;

    location / {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

---

## Yol B — Docker Compose

Docker Desktop veya Docker Engine kuruluysa:

```bash
# 1. Ortam değişkenlerini ayarlayın
cp .env.example .env
# .env dosyasını bir metin editörüyle açıp şifreleri değiştirin

# 2. Başlatın
docker compose up -d

# 3. Durumu kontrol edin
docker compose ps
docker compose logs app
```

Uygulama `http://sunucu-adresi:8080` adresinde çalışır.

---

## Yol C — Linux Servis (systemd)

```bash
# 1. .NET 8 SDK kur (derleme için SDK, çalıştırma için Runtime yeterlidir)
# Ubuntu/Debian:
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update && sudo apt install -y dotnet-sdk-8.0

# 2. Uygulama klasörü oluştur
sudo mkdir -p /opt/redzone /var/lib/redzone

# 3. Kaynak kodu kopyala ve derle
# (Bu rehberi indirdiğiniz dizinde olduğunuzu varsayıyoruz)
sudo cp -r . /opt/redzone/src
cd /opt/redzone/src
dotnet publish RiskManagement/RiskManagement.csproj -c Release -o /opt/redzone/app

# 4. Yapılandırma
sudo cp RiskManagement/appsettings.Intranet.example.json /opt/redzone/app/appsettings.Production.json
# /opt/redzone/app/appsettings.Production.json dosyasını düzenleyin:
# - Jwt.Key: rastgele uzun bir anahtar girin
# - AppSettings.SqlitePath: "/var/lib/redzone/redzone.db"
# - Urls: "http://0.0.0.0:5000"

# 5. systemd servis dosyası
sudo tee /etc/systemd/system/redzone.service << 'UNIT'
[Unit]
Description=RedZone Risk Yönetim Sistemi
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/redzone/app
ExecStart=/usr/bin/dotnet /opt/redzone/app/RiskManagement.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=redzone
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
UNIT

# 6. Başlat
sudo systemctl daemon-reload
sudo systemctl enable redzone
sudo systemctl start redzone
sudo systemctl status redzone
```

---

## Yol D — Hızlı Test (Demo)

Sunucuya kurmadan önce masaüstünüzde denemek için:

> **Ön koşul:** [.NET 8 **SDK**](https://dotnet.microsoft.com/download/dotnet/8) kurulu olmalıdır (Runtime değil, SDK).

```powershell
# Windows PowerShell
cd risk-management-dotnet\RiskManagement
dotnet run --environment Development
# Tarayıcıda: http://localhost:5000
# Kullanıcı: admin / Admin123!
```

Demo modda gerçekçi örnek verilerle sistem çalışır.  
Veriler `risk_management.db` dosyasında saklanır, dilediğinizde silebilirsiniz.

---

## Active Directory (LDAP) Entegrasyonu

Çalışanların mevcut AD şifresiyle giriş yapması için:

1. Admin paneli → Sistem Ayarları → LDAP Yapılandırması
2. AD sunucunuzun bilgilerini girin:
   - Sunucu: `ldap://dc.sirketiniz.local`
   - Base DN: `DC=sirketiniz,DC=local`
   - Bağlantı şifresi: servis hesabı şifresi

AD kullanıcıları ilk girişte otomatik oluşturulur, rol ataması admin tarafından yapılır.

---

## Yedekleme

### Windows Kurulumu (SQLite)

Tüm veriler tek bir dosyada: `C:\ProgramData\RedZone\redzone.db`

**Otomatik yedekleme (Windows Görev Zamanlayıcısı):**

```powershell
# Her gece 02:00'de yedek al
$action  = New-ScheduledTaskAction -Execute "PowerShell.exe" `
    -Argument "-Command `"Copy-Item 'C:\ProgramData\RedZone\redzone.db' 'C:\Yedek\redzone_$(Get-Date -f yyyyMMdd).db'`""
$trigger = New-ScheduledTaskTrigger -Daily -At 2am
Register-ScheduledTask -TaskName "RedZone Yedek" -Action $action -Trigger $trigger -RunLevel Highest
```

### Docker / Linux Kurulumu (MySQL)

```bash
# MySQL veritabanı yedeği
docker exec red-db mysqldump -u riskapp -p"$DB_PASSWORD" \
  --single-transaction RiskManagement | gzip > backup_$(date +%Y%m%d).sql.gz

# Uploads klasörü yedeği
docker cp red-app:/app/uploads ./backup_uploads_$(date +%Y%m%d)

# Geri yükleme
gunzip -c backup_20260101.sql.gz | docker exec -i red-db \
  mysql -u riskapp -p"$DB_PASSWORD" RiskManagement
```

Kapsamlı yedekleme kılavuzu için: [docs/BACKUP_RESTORE.md](docs/BACKUP_RESTORE.md)

---

## Sorun Giderme

| Sorun | Çözüm |
|-------|-------|
| Tarayıcıda açılmıyor | `Get-Service RedZone` ile servisi kontrol edin |
| "Bağlantı reddedildi" | Güvenlik duvarını kontrol edin: `netsh advfirewall firewall show rule name="RedZone*"` |
| Servis hemen kapanıyor | Olay Görüntüleyici → Windows Günlükleri → Uygulama |
| Veritabanı hatası | `C:\ProgramData\RedZone` klasörünün yazma izni var mı? |
| Şifremi unuttum | Veritabanından sıfırlayın (aşağıya bakın) |

**Admin şifresini unuttuysanız** (başka admin yoksa):

```sql
-- MySQL için: yeni şifre hash'ini oluşturup güncelleyin
-- Hash'i aşağıdaki komutla üretebilirsiniz:
--   dotnet run -- hash-password YeniSifre123!
UPDATE Users SET PasswordHash = '<hash>', FailedLoginCount = 0, LockoutUntil = NULL
WHERE Username = 'admin';
```

SQLite için aynı SQL'i şu araçla çalıştırabilirsiniz:
```powershell
# SQLite CLI ile (winget install SQLite.SQLite)
sqlite3 "C:\ProgramData\RedZone\redzone.db" "UPDATE Users SET FailedLoginCount=0, LockoutUntil=NULL WHERE Username='admin';"
```
Ardından uygulamayı yeniden `DemoMode=true` ile başlatıp admin şifresini uygulama içinden değiştirin.

**Günlüklere bakmak için:**
```powershell
# Son 50 satır
Get-EventLog -LogName Application -Source RedZone -Newest 50
```

---

## Güncelleme

```powershell
# Yeni sürümü aynı klasöre kopyalayın, sonra:
Stop-Service RedZone
# Dosyaları güncelleyin (C:\Program Files\RedZone üzerine)
Start-Service RedZone
```

Veritabanı güncelleme gerektiren sürümlerde migrasyon otomatik çalışır.

---

*Sorun mu yaşıyorsunuz? GitHub Issues: https://github.com/ozgurakkaya90/redzone/issues*
