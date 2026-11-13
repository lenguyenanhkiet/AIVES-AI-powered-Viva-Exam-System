using AIpoweredVivaExamSystem.Business.Exams;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.ExamFoundation;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel(ExamFoundationService foundation) : PageModel
{
    public IReadOnlyList<AIpoweredVivaExamSystem.DataAccess.Models.Exams.ExamSummary> Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Vm = await foundation.ListAsync(ct);
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
