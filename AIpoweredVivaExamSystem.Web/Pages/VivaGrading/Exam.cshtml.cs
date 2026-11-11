using AIpoweredVivaExamSystem.Application.VivaSessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIpoweredVivaExamSystem.Web.Pages.VivaGrading;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ExamModel(IVivaGradingService grading) : PageModel
{
    public ExamGradingOverview Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not { } examId || examId == Guid.Empty) return NotFound();
        Vm = await grading.GetExamOverviewAsync(examId, ct);
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
