# Katkıda Bulunma Rehberi

RedZone'a katkıda bulunduğunuz için teşekkürler. Aşağıdaki kurallar projeyi tutarlı tutmak içindir.

## Nasıl Başlanır

```bash
git clone https://github.com/your-org/risk-management-dotnet.git
cd risk-management-dotnet
cd RiskManagement && dotnet run   # SQLite ile çalışır, ek kurulum gerekmez
```

Testleri çalıştırmak için:

```bash
cd RiskManagement.Tests && dotnet test
```

## Katkı Süreci

1. Bir issue açın veya mevcut bir issue'yu üstlenin.
2. `feature/kısa-açıklama` veya `fix/kısa-açıklama` formatında branch oluşturun.
3. Değişikliğinizi yapın ve testleri geçtiğinden emin olun.
4. PR açın — başlık Türkçe veya İngilizce olabilir.

## Kod Standartları

- **.NET 8 C# 12** — language features kullanımı teşvik edilir (primary constructors, collection expressions vb.)
- **Blazor Server** — `OnInitializedAsync` tercih edilir; `StateHasChanged` yalnızca gerektiğinde
- **EF Core** — ham SQL yerine LINQ; migration'lar yalnızca MySQL ile test edilmeli
- **Yorum yok** — kod kendini açıklamalı; neden anlaşılmıyorsa kısa bir yorum eklenebilir
- Yeni özellikler için servis katmanında test yazılması beklenir

## Güvenlik Notları

- E-posta şifresi `AppConfig` tablosunda düz metin JSON olarak saklanır. Üretimde veritabanı erişimini kısıtlayın.
- Seed verisi rastgele şifreler üretir; bu şifreler yalnızca ilk başlatmada konsola yazdırılır.
- Güvenlik açığı bulduysanız lütfen önce issue açmak yerine doğrudan maintainer ile iletişime geçin.

## Proje Felsefesi

RedZone, büyük kurumsal GRC araçlarının KOBİ'lere uyarlanmış açık kaynak alternatifidir. Alan uzmanlığı (Fine-Kinney, iç denetim metodolojisi) teknik kaliteden önce gelir. Yeni özellik önerirken önce bir issue açıp tartışın.
