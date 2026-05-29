# Güvenlik Politikası

## Desteklenen Sürümler

Bu proje aktif geliştirme aşamasındadır. Güvenlik düzeltmeleri yalnızca en son sürüm (`main`
dalı ve en güncel yayın) için sağlanır.

| Sürüm | Destek |
|-------|--------|
| En son yayın / `main` | ✅ |
| Önceki sürümler | ❌ |

## Güvenlik Açığı Bildirme

Bir güvenlik açığı keşfederseniz, lütfen **herkese açık bir GitHub issue açmayın.**
Açığın sorumlu bir şekilde ele alınabilmesi için aşağıdaki yolu izleyin:

1. GitHub üzerinde **Security → Report a vulnerability** (Private vulnerability reporting)
   özelliğini kullanarak özel bir bildirim oluşturun.
2. Alternatif olarak depo sahibiyle özel kanaldan iletişime geçin.

Bildiriminizde mümkünse şunları paylaşın:

- Açığın türü ve etkisi (ör. yetki yükseltme, veri sızıntısı, RCE).
- Etkilenen dosya/uç nokta ve nasıl tetiklendiği.
- Yeniden üretmek için adımlar veya bir kanıt (PoC).
- Varsa önerilen düzeltme.

## Süreç

- Bildiriminizi makul bir süre içinde değerlendirip geri dönüş yapmaya çalışırız.
- Açık doğrulandığında bir düzeltme hazırlanır ve yayınlanır.
- Sorumlu açıklama (responsible disclosure) ilkesine uyan bildirimciler, istemeleri hâlinde
  sürüm notlarında teşekkürle anılır.

## Kapsam Dışı

- Otomatik tarayıcıların doğrulanmamış raporları.
- Sosyal mühendislik ve fiziksel erişim gerektiren senaryolar.
- Varsayılan demo verisi/şifreleri ile çalışan, üretim için yapılandırılmamış kurulumlar
  (bkz. [README → Güvenlik](README.md#güvenlik) üretim kontrol listesi).

> Not: Bu yazılım MIT lisansı kapsamında "olduğu gibi" sunulur; herhangi bir güvenlik
> garantisi vermez. Üretim kullanımından önce kendi değerlendirmenizi yapın.
