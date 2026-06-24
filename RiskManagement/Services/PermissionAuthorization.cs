using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace RiskManagement.Services;

/// <summary>
/// Sayfa erişimini, sabit rol listesi yerine atanan izinlere (Yetki Yönetimi)
/// göre denetlemek için kullanılan yetki gereksinimi.
/// Kullanımı: @attribute [Authorize(Policy = "ethics.write")]
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}

/// <summary>
/// PermissionRequirement'ı, kullanıcının rollerinden hesaplanan izin kümesine
/// karşı doğrular. Böylece menü görünürlüğü ile sayfa erişimi aynı kaynağı kullanır.
/// </summary>
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly AuthService _auth;

    public PermissionHandler(AuthService auth) => _auth = auth;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Count > 0 && _auth.HasPermissionByRoles(roles, requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
