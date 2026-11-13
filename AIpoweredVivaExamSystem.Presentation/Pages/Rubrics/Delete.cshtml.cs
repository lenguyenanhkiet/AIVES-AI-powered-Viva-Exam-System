using AIpoweredVivaExamSystem.Business.Rubrics;
using AIpoweredVivaExamSystem.Business.Questions;
using AIpoweredVivaExamSystem.DataAccess.Enums;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Rubrics;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DeleteModel(RubricService rubrics, QuestionService questions) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        var rubric = await rubrics.GetAsync(id, cancellationToken);
        // An approved question must keep its rubric; it has to go back to Draft (by editing) first.
        var question = await questions.GetAsync(rubric.QuestionId, cancellationToken);
        if (question.Status == QuestionStatus.Approved)
        {
            TempData.Toast("Không thể xóa rubric của câu hỏi đã duyệt. Sửa câu hỏi để đưa về Nháp trước.", success: false);
            return RedirectToPage("/Questions/Details", new { id = rubric.QuestionId });
        }
        await rubrics.DeleteAsync(id, cancellationToken);
        TempData.Toast("Đã xóa rubric.");
        return RedirectToPage("/Questions/Details", new { id = rubric.QuestionId });
    }

    public IActionResult OnGet() => NotFound();
}
