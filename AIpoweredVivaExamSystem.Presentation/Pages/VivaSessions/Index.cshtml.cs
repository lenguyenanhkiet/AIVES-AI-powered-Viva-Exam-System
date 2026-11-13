using AIpoweredVivaExamSystem.Business.VivaSessions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Presentation.Common;

namespace AIpoweredVivaExamSystem.Presentation.Pages.VivaSessions;

[Authorize(Roles = "STUDENT")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IndexModel(MyVivaSessionsService mySessions) : PageModel
{
    public IReadOnlyList<AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions.MyVivaExam> Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Vm = await mySessions.ListAsync(ct);
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
