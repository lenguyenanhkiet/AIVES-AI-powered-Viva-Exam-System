using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using AIpoweredVivaExamSystem.Presentation.Common;

namespace AIpoweredVivaExamSystem.Presentation.Pages.Home;

[AllowAnonymous, ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ErrorModel : PageModel
{
    public ErrorViewModel Vm { get; private set; } = default!;

    public IActionResult OnGet()
    {
        Vm = new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier };
        return Page();
    }

    public IActionResult OnPost() => OnGet();
}
