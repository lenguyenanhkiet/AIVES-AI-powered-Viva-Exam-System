using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.VivaSessions;

[Authorize(Roles = "STUDENT")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IndexModel(AIpoweredVivaExamSystem.Application.VivaSessions.IMyVivaSessionsReader mine) : PageModel
{
    public IReadOnlyList<AIpoweredVivaExamSystem.Application.VivaSessions.MyVivaExam> Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Vm = await mine.ListAsync(ct);
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
