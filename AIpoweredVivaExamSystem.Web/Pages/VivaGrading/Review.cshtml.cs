using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIpoweredVivaExamSystem.Web.Pages.VivaGrading;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ReviewModel(IVivaGradingService grading) : PageModel
{
    public VivaSessionGradingSheet Sheet { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not { } sessionId || sessionId == Guid.Empty) return NotFound();
        Sheet = await grading.GetSessionSheetAsync(sessionId, autoGenerateMissingAi: true, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostRegradeAsync(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) return NotFound();
        try
        {
            await grading.RunAiGradingAsync(id, forceRegrade: true, ct);
            TempData.Toast("AI đã phân tích và cập nhật điểm gợi ý theo rubric cho toàn bộ câu hỏi.");
        }
        catch (Exception ex) when (ex is ResourceConflictException or InvalidOperationException || ex.GetType().Name == "DomainValidationException")
        {
            TempData.Toast(ex.Message, success: false);
        }

        return RedirectToPage("/VivaGrading/Review", new { id });
    }

    public async Task<IActionResult> OnPostSaveReviewAsync(
        Guid id,
        Guid sessionQuestionId,
        decimal finalScore,
        string? comment,
        CancellationToken ct)
    {
        if (id == Guid.Empty || sessionQuestionId == Guid.Empty) return NotFound();
        try
        {
            await grading.SaveTeacherReviewAsync(
                new SaveTeacherReviewRequest(id, sessionQuestionId, finalScore, comment),
                ct);
            TempData.Toast("Đã lưu quyết định điểm cuối cùng của giảng viên.");
        }
        catch (Exception ex) when (ex is ResourceConflictException or InvalidOperationException || ex.GetType().Name == "DomainValidationException")
        {
            TempData.Toast(ex.Message, success: false);
        }

        return RedirectToPage("/VivaGrading/Review", new { id });
    }

    public async Task<IActionResult> OnPostApproveAllAsync(Guid id, string? comment, CancellationToken ct)
    {
        if (id == Guid.Empty) return NotFound();
        try
        {
            await grading.ApproveAllAiScoresAsync(id, comment, ct);
            TempData.Toast("Đã xác nhận và chốt toàn bộ điểm theo mức điểm AI gợi ý.");
        }
        catch (Exception ex) when (ex is ResourceConflictException or InvalidOperationException || ex.GetType().Name == "DomainValidationException")
        {
            TempData.Toast(ex.Message, success: false);
        }

        return RedirectToPage("/VivaGrading/Review", new { id });
    }
}
