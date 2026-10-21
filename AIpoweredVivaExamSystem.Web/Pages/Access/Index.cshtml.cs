using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.Access;

[Authorize(Roles = "ADMIN")]
public sealed class IndexModel(AccessService access) : PageModel
{
    public AIpoweredVivaExamSystem.Application.Authentication.AccessOverview Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Vm = await access.OverviewAsync(ct);
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
