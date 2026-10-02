using System.Security.Claims;
using AIpoweredVivaExamSystem.Application.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AIpoweredVivaExamSystem.Web.Authentication;

/// <summary>Cookie hợp lệ về chữ ký vẫn phải thuộc một tài khoản còn hoạt động trong SQL.</summary>
public sealed class MvcCookieEvents(LoginService login) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            && await login.IsSessionActiveAsync(id, context.HttpContext.RequestAborted)) return;
        // Reject trước Authorization, đồng thời xóa cookie cũ để request tiếp theo không dùng lại ticket bị thu hồi.
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
