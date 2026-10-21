using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.ExamFoundation;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DetailsModel(ExamAccessGuard guard, AIpoweredVivaExamSystem.Application.VivaSessions.IExamLiveReader live) : PageModel
{
    public AIpoweredVivaExamSystem.Application.Exams.ExamSummary Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        await guard.CanManageExamAsync(id, ct);
        Vm = await live.SummaryAsync(id, ct);
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
