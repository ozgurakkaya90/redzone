# Yol Haritası

Bu belge projenin yönünü özetler. Öncelikler topluluk geri bildirimiyle değişebilir;
katkı için [CONTRIBUTING.md](CONTRIBUTING.md)'e bakın.

## Yapıldı ✅
- Risk (Fine-Kinney), iç denetim, dış denetim, etik bildirim modülleri
- Rol/izin tabanlı erişim (9 rol, 50+ izin), tam denetim izi
- Excel/PDF dışa aktarım (tamamen permissif PDF kütüphanesi — MigraDocCore)
- LDAP/AD entegrasyonu, anonim etik bildirim + IP rate limiting
- KVKK/GDPR anonimleştirme (unutulma hakkı) UI'dan erişilebilir
- 390+ otomatik test, CI (MySQL ile build+test)

## Kısa vade 🎯
- Etik bildirim için direktif SLA zamanlayıcıları (alındı teyidi / geri bildirim süreleri)
- Risk-bazlı denetim planlaması bağının güçlendirilmesi
- MCP kapsam izolasyonu ve import yetki-sınırı için ek güvenlik testleri
- Ekran görüntüleri / canlı demo bağlantısı (README)

## Orta vade 🔭
- Konfigüre edilebilir risk metodolojisi (Fine-Kinney dışı: Olasılık × Etki 5×5 matris)
- Sözlük tabloları ile referans bütünlüğü (kategori, strateji, kontrol türü vb.)
- Çok dilli arayüz (i18n) — şu an Türkçe odaklı
- Raporlama panoları ve dışa aktarım şablonları

## Uzun vade 🚀
- Eklenti/genişletme mimarisi
- SSO (OIDC/SAML)
- Mobil uyumlu görünüm iyileştirmeleri

## Katkı
"good first issue" etiketli görevler yeni katkıcılar için uygundur. Öneri ve hata
bildirimleri için GitHub Issues kullanın.
