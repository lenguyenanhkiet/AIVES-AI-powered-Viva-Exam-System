using System.Security.Claims;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.Account;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[AllowAnonymous]
public sealed class LoginModel(LoginService login) : ContentPageModel
{
    public IActionResult OnGet(string? returnUrl = null) => RenderContent("/Pages/Account/_LoginContent.cshtml", new LoginViewModel { ReturnUrl = returnUrl });

    public async Task<IActionResult> OnPostAsync(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return RenderContent("/Pages/Account/_LoginContent.cshtml", model);
        var user = await login.AuthenticateAsync(model.Email, model.Password, cancellationToken);
        if (user is null)
        {
            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            return RenderContent("/Pages/Account/_LoginContent.cshtml", model);
        }
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Name, user.FullName)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        // Cookie là ticket được ASP.NET Core bảo vệ; không lưu mật khẩu hoặc JWT trong HTML/localStorage.
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        // Chỉ cho redirect nội bộ để URL do người dùng nhập không chuyển phiên sang website khác.
        return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/");
    }
}
