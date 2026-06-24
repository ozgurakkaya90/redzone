using RiskManagement.Services.Email;
using Xunit;

namespace RiskManagement.Tests;

/// <summary>
/// EmailTemplates statik metodlarının güvenlik ve içerik doğrulaması.
/// - Şablon HTML döndürmeli
/// - null baseUrl ile crash etmemeli
/// - Çıktı script tag içermemeli (XSS koruması)
/// - Custom subject/body override çalışmalı
/// </summary>
public class EmailTemplateTests
{
    private const string BaseUrl = "https://risk.example.com";

    // ── RiskProposed ─────────────────────────────────────────────────────────

    [Fact]
    public void RiskProposed_DefaultTemplate_ContainsRiskCode()
    {
        var (subject, body) = EmailTemplates.RiskProposed("R-001", "Test Riski", "Ayşe", BaseUrl);
        Assert.Contains("R-001", subject + body);
        Assert.Contains("Test Riski", body);
    }

    [Fact]
    public void RiskProposed_CustomTemplate_UsesCustomSubject()
    {
        var (subject, _) = EmailTemplates.RiskProposed(
            "R-002", "Başlık", "Kullanıcı", BaseUrl,
            customSubject: "Özel Konu: {riskCode}");
        Assert.Contains("R-002", subject);
        Assert.Contains("Özel Konu", subject);
    }

    // ── StatusChanged ────────────────────────────────────────────────────────

    [Fact]
    public void StatusChanged_ReturnsNonEmptyHtml()
    {
        var (subject, body) = EmailTemplates.StatusChanged(
            "R-001", "Başlık", "Önerildi", "Onaylandı", BaseUrl);
        Assert.NotEmpty(subject);
        Assert.Contains("<html", body);
    }

    [Fact]
    public void StatusChanged_ContainsBothStatuses()
    {
        var (_, body) = EmailTemplates.StatusChanged(
            "R-001", "Başlık", "Önerildi", "İncelemede", BaseUrl);
        Assert.Contains("Önerildi", body);
        Assert.Contains("İncelemede", body);
    }

    // ── OwnerAssigned ────────────────────────────────────────────────────────

    [Fact]
    public void OwnerAssigned_ContainsBaseUrlLink()
    {
        var (_, body) = EmailTemplates.OwnerAssigned("R-001", "Başlık", BaseUrl);
        Assert.Contains(BaseUrl, body);
    }

    // ── ActionDueSoon ────────────────────────────────────────────────────────

    [Fact]
    public void ActionDueSoon_ContainsDaysLeft()
    {
        var (_, body) = EmailTemplates.ActionDueSoon(
            "R-001", "Aksiyon", "01.06.2026", 5, BaseUrl);
        Assert.Contains("5", body);
    }

    // ── Test e-postası ───────────────────────────────────────────────────────

    [Fact]
    public void Test_ReturnsValidHtml()
    {
        var (subject, body) = EmailTemplates.Test(BaseUrl);
        Assert.NotEmpty(subject);
        Assert.Contains("<!DOCTYPE html>", body);
    }

    // ── HTML injection — kullanıcı girdisi gövdeye ENCODE edilmeli ───────────
    // Regresyon: ApplyVars değerleri ham gömüyordu. Risk başlığı/öneren adı gibi
    // kullanıcı-kontrollü serbest metin (anonim risk önerisinde kimliksiz kullanıcı
    // dahil) IsBodyHtml=true bildirim e-postasına HTML/phishing enjekte edebiliyordu.

    [Fact]
    public void RiskProposed_EncodesHtmlInUserFields()
    {
        var (_, body) = EmailTemplates.RiskProposed(
            "R-001", "<script>alert(1)</script>", "<b>Sahte Öneren</b>", BaseUrl);
        Assert.DoesNotContain("<script>alert(1)</script>", body); // ham enjeksiyon yok
        Assert.Contains("&lt;script&gt;", body);                  // encode edildi (strip değil)
        Assert.DoesNotContain("<b>Sahte Öneren</b>", body);
    }

    [Fact]
    public void EthicsSubmitted_EncodesHtmlInSubject()
    {
        // Anonim ihbar konusu kullanıcı-kontrollü; iç personele giden e-postaya enjekte olmamalı.
        var (_, body) = EmailTemplates.EthicsSubmitted(
            "EB-001", "<img src=x onerror=alert(1)>", BaseUrl);
        Assert.DoesNotContain("<img src=x", body);
        Assert.Contains("&lt;img", body);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<a href=\"http://phish\">tıkla</a>")]
    public void ActionDueSoon_EncodesHtmlInDescription(string payload)
    {
        var (_, body) = EmailTemplates.ActionDueSoon("R-001", payload, "01.06.2026", 5, BaseUrl);
        Assert.DoesNotContain(payload, body);
        Assert.Contains("&lt;", body);
    }

    // ── null/boş baseUrl edge-case ───────────────────────────────────────────

    [Fact]
    public void RiskProposed_NullBaseUrl_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            EmailTemplates.RiskProposed("R-001", "Başlık", "Kullanıcı", null!));
        Assert.Null(ex);
    }

    [Fact]
    public void ActionDueSoon_EmptyBaseUrl_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            EmailTemplates.ActionDueSoon("R-001", "Aksiyon", "01.06.2026", 3, ""));
        Assert.Null(ex);
    }
}
