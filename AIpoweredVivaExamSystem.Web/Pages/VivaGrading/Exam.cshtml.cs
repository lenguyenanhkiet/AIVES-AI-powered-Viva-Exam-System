using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIpoweredVivaExamSystem.Web.Pages.VivaGrading;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ExamModel(IVivaGradingService grading) : PageModel
{
    public ExamGradingOverview Vm { get; private set; } = default!;
    public VivaGradingStatus? StatusFilter { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid? id, VivaGradingStatus? status, CancellationToken ct)
    {
        if (id is not { } examId || examId == Guid.Empty) return NotFound();
        StatusFilter = status;
        Vm = await grading.GetExamOverviewAsync(examId, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostBatchAiGradeAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) return NotFound();
        try
        {
            var gradedCount = await grading.RunExamBatchAiGradingAsync(id, ct);
            if (gradedCount > 0)
            {
                TempData.Toast($"AI đã phân tích và chấm điểm gợi ý cho {gradedCount} phiên thi.");
            }
            else
            {
                TempData.Toast("Tất cả các phiên thi mở đều đã có điểm gợi ý từ AI.");
            }
        }
        catch (Exception ex) when (ex is ResourceConflictException or InvalidOperationException || ex.GetType().Name == "DomainValidationException")
        {
            TempData.Toast(ex.Message, success: false);
        }

        return RedirectToPage("/VivaGrading/Exam", new { id });
    }
}
