using AIpoweredVivaExamSystem.Business.VivaSessions;
using AIpoweredVivaExamSystem.Business.Exams;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.ExamFoundation;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DetailsModel(ExamAccessGuard examGuard, ExamLiveService liveExams) : PageModel
{
    public AIpoweredVivaExamSystem.DataAccess.Models.Exams.ExamSummary Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        await examGuard.CanManageExamAsync(id, ct);
        Vm = await liveExams.SummaryAsync(id, ct);
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
