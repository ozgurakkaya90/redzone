using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;

namespace RiskManagement.Services;

/// <summary>
/// Blazor Server'da IHttpContextAccessor, SignalR circuit (interaktif) fazında
/// null döner. Bu yüzden IHttpContextAccessor ile oturum kullanıcısını okuyan
/// sayfalar interaktif fazda kullanıcıyı göremez ("Yetki yok", menü kaybolması,
/// sayfa açılmaması gibi belirtiler).
///
/// Bu handler, her circuit aktivitesinde (render/event) oturum kullanıcısını
/// AuthenticationState'ten alıp IHttpContextAccessor.HttpContext üzerinden
/// erişilebilir kılar. Böylece mevcut sayfalar değişmeden doğru kullanıcıyı görür.
/// Gerçek HTTP istekleri (login, prerender, API) bundan etkilenmez.
/// </summary>
public sealed class UserCircuitHandler : CircuitHandler
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserCircuitHandler(
        AuthenticationStateProvider authStateProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _authStateProvider = authStateProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
    {
        return async context =>
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            _httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                User = authState.User
            };
            await next(context);
        };
    }
}
