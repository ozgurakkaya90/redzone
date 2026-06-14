# Üçüncü Taraf Lisansları

Bu proje (MIT lisanslı) aşağıdaki açık kaynak bileşenleri kullanır. Tüm bağımlılıklar
**permissif** lisanslıdır; ticari kullanım dahil yeniden dağıtıma izin verir.

| Bileşen | Kullanım | Lisans |
|---------|----------|--------|
| ClosedXML | Excel dışa/içe aktarma | MIT |
| MigraDocCore.Rendering / DocumentObjectModel | PDF rapor üretimi | MIT |
| PdfSharpCore | PDF render motoru (MigraDoc altında) | MIT |
| SixLabors.ImageSharp | Görsel/glif işleme (PDF) | Apache-2.0 |
| SixLabors.Fonts | Font işleme (PDF) | Apache-2.0 |
| BCrypt.Net-Next | Parola hash'leme | MIT |
| Pomelo.EntityFrameworkCore.MySql | MySQL EF Core sağlayıcısı | MIT |
| Novell.Directory.Ldap.NETStandard | LDAP/Active Directory | MIT |
| ModelContextProtocol.AspNetCore | MCP sunucusu | MIT |
| Microsoft.EntityFrameworkCore.* | ORM ve araçlar | MIT |
| **Lato** (gömülü font) | PDF tipografi | SIL Open Font License 1.1 |

## Notlar
- **PDF kütüphanesi:** Proje, lisans tieri olmayan tamamen permissif (MIT) MigraDocCore/PdfSharpCore
  kullanır. (Daha önce kullanılan QuestPDF Community lisansı yalnızca belirli ciro eşiğinin altındaki
  kuruluşlar için ücretsiz olduğundan, ticari kullanımı sınırsız serbest bırakmak için kaldırılmıştır.)
- **Lato fontu** SIL OFL 1.1 ile dağıtılır; uygulama içine gömülerek (embedded) tüm platformlarda
  tutarlı PDF çıktısı sağlar. OFL, fontun yazılımla birlikte paketlenmesine izin verir.

Her bileşenin tam lisans metni ilgili NuGet paketinde / proje deposunda yer alır.
