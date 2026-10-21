using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.ExamFoundation;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel(ExamFoundationService foundation) : PageModel
{
    public IReadOnlyList<AIpoweredVivaExamSystem.Application.Exams.ExamSummary> Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Vm = await foundation.ListAsync(ct);
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
