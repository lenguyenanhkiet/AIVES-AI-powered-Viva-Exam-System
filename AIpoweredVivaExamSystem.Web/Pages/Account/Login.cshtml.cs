using Microsoft.AspNetCore.Mvc.RazorPages;
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
public sealed class LoginModel(LoginService login) : PageModel
{
    [BindProperty]
    public LoginViewModel Input { get; set; } = new();

    public IActionResult OnGet(string? returnUrl = null)
    {
        Input = new LoginViewModel { ReturnUrl = returnUrl };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var user = await login.AuthenticateAsync(Input.Email, Input.Password, cancellationToken);
        if (user is null)
        {
            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            return Page();
        }
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Name, user.FullName)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        // Cookie là ticket được ASP.NET Core bảo vệ; không lưu mật khẩu hoặc JWT trong HTML/localStorage.
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        // Chỉ cho redirect nội bộ để URL do người dùng nhập không chuyển phiên sang website khác.
        return LocalRedirect(Url.IsLocalUrl(Input.ReturnUrl) ? Input.ReturnUrl! : "/");
    }
}
