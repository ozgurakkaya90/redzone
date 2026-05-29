using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RiskManagement.Pages;

// İşlenmeyen sunucu hatalarında UseExceptionHandler("/Error") buraya yönlendirir.
// Anonim erişime açık: hata, kimliği doğrulanmamış istekte de oluşabilir.
[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel(ILogger<ErrorModel> logger) : PageModel
{
    // Destek talebinde kullanıcının verebileceği korelasyon kimliği.
    public string RequestId { get; private set; } = "";

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        // Orijinal istek yolu ve istisnayı korelasyon kimliğiyle birlikte logla.
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (feature?.Error != null)
        {
            logger.LogError(feature.Error,
                "İşlenmeyen istisna — yol: {Path}, RequestId: {RequestId}",
                feature.Path, RequestId);
        }
    }
}
