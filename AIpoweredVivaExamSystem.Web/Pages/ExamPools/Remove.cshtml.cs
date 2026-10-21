using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels.ExamPools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.ExamPools;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class RemoveModel(ExamPoolService pools) : ContentPageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, ExamPoolSelectionViewModel form, CancellationToken ct)
    {
        var page = await PageAsync(id, form, ct);
        if (!ModelState.IsValid) return RenderContent("/Pages/ExamPools/_IndexContent.cshtml", page);
        try
        {
            await pools.RemoveAsync(id, form.QuestionIds, ct);
            TempData.Toast("Đã bỏ câu hỏi khỏi pool.");
            return RedirectToPage("/ExamPools/Index", new { id });
        }
        catch (ResourceConflictException ex)
        {
            TempData.Toast(ex.Message, success: false);
            return RedirectToPage("/ExamPools/Index", new { id });
        }
        catch (DomainValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return RenderContent("/Pages/ExamPools/_IndexContent.cshtml", await PageAsync(id, form, ct));
        }
    }

    private async Task<ExamPoolViewModel> PageAsync(Guid examId, ExamPoolSelectionViewModel form, CancellationToken ct) =>
        new() { Page = await pools.GetPageAsync(examId, ct), Form = form };

    public IActionResult OnGet() => NotFound();
}
