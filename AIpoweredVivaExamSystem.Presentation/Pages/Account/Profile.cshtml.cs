using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Presentation.Common;

namespace AIpoweredVivaExamSystem.Presentation.Pages.Account;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[Authorize]
public sealed class ProfileModel : PageModel
{
    public IActionResult OnGet() => Page();

    public IActionResult OnPost() => NotFound();
}
