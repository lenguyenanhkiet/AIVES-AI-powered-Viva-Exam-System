using AIpoweredVivaExamSystem.Application.Exams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIpoweredVivaExamSystem.Web.Pages.VivaGrading;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel(ExamService exams) : PageModel
{
    public IReadOnlyList<ExamSummary> Exams { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Exams = await exams.ListManagedAsync(ct);
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
