using System.Security.Claims;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Web.Controllers;

/// <summary>Presentation MVC: nhận form, gọi LoginService và quản lý cookie; không truy vấn DB trực tiếp.</summary>
public sealed class AccountController(LoginService login) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    /// <summary>Xác thực form bằng antiforgery; tạo phiên trình duyệt chỉ sau khi nghiệp vụ xác nhận tài khoản.</summary>
    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await login.AuthenticateAsync(model.Email, model.Password, cancellationToken);
        if (user is null)
        {
            ModelState.AddModelError("", "Email hoặc mật khẩu không đúng.");
            return View(model);
        }
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Name, user.FullName)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        // Cookie là ticket được ASP.NET Core bảo vệ; không lưu mật khẩu hoặc JWT trong HTML/localStorage.
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        // Chỉ cho redirect nội bộ để URL do người dùng nhập không chuyển phiên sang website khác.
        return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/");
    }

    [Authorize, HttpGet]
    public IActionResult Profile() => View();

    /// <summary>Logout chỉ qua POST có antiforgery; xóa ticket cookie của trình duyệt hiện tại.</summary>
    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
