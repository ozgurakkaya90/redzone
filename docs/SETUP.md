# Risk Yönetim Sistemi — .NET 8 + Blazor Server + MSSQL

## Gereksinimler

- .NET 8 SDK
- SQL Server 2019+ (veya Azure SQL / LocalDB)
- Visual Studio 2022 veya VS Code + C# Dev Kit

## Kurulum

### 1. Bağlantı dizesi ayarlama

`RiskManagement/appsettings.json` dosyasındaki connection string'i düzenleyin:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=RiskManagement;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Ya da ortam değişkeni olarak:
```bash
export ConnectionStrings__DefaultConnection="Server=...;Database=RiskManagement;..."
```

### 2. Migration çalıştırma

```bash
cd RiskManagement
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 3. Çalıştırma

```bash
dotnet run
# → https://localhost:5001 adresinden açılır
```

## Varsayılan Kullanıcılar (Demo Mode)

| Kullanıcı    | Şifre       | Rol            |
|--------------|-------------|----------------|
| admin        | admin123    | Yönetici       |
| komite1      | komite123   | Risk Komitesi  |
| riskowner1   | owner123    | Risk Sahibi    |
| denetci1     | denetci123  | Denetçi        |
| denetimmgr   | manager123  | Denetim Müdürü |

## Docker

```bash
docker build -t risk-management .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Server=host.docker.internal;Database=RiskManagement;..." \
  risk-management
```

## Railway Deploy

`railway.toml`:
```toml
[build]
builder = "DOCKERFILE"
dockerfilePath = "Dockerfile"

[deploy]
healthcheckPath = "/"
```

Environment variable olarak ekleyin:
```
ConnectionStrings__DefaultConnection = Server=...
AppSettings__DemoMode = false
```

## Proje Yapısı

```
RiskManagement/
├── Models/          → EF Core entity'leri
├── Data/
│   ├── AppDbContext.cs    → DB context
│   └── SeedData.cs        → Demo veriler
├── Services/
│   ├── AuthService.cs     → Kimlik doğrulama, rol/yetki
│   ├── ConfigService.cs   → Sistem yapılandırması
│   ├── RiskService.cs     → Risk iş mantığı
│   ├── AuditService.cs    → Denetim iş mantığı
│   └── EthicsService.cs   → Etik bildirim iş mantığı
├── Pages/           → Blazor sayfaları (.razor)
│   ├── Risk/
│   ├── Audit/
│   ├── Ethics/
│   └── Admin/
├── Shared/          → Layout, bileşenler
│   └── Components/
└── wwwroot/css/     → Stiller
```
