using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using AIpoweredVivaExamSystem.Presentation.Common;

namespace AIpoweredVivaExamSystem.Presentation.Pages.Home;

[AllowAnonymous]
public sealed class StatusCodeModel : PageModel
{
    public int Vm { get; private set; } = default!;

    public IActionResult OnGet(int code)
    {
        Response.StatusCode = code;
        Vm = code;
        return Page();
    }

    public IActionResult OnPost(int code) => OnGet(code);
}
