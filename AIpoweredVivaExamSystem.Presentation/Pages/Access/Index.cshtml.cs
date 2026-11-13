using AIpoweredVivaExamSystem.Business.Authentication;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Presentation.Common;

namespace AIpoweredVivaExamSystem.Presentation.Pages.Access;

[Authorize(Roles = "ADMIN")]
public sealed class IndexModel(AccessService access) : PageModel
{
    public AIpoweredVivaExamSystem.DataAccess.Models.Authentication.AccessOverview Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Vm = await access.OverviewAsync(ct);
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
