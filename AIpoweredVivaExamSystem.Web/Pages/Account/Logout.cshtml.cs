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
[Authorize]
public sealed class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }

    public IActionResult OnGet() => NotFound();
}
