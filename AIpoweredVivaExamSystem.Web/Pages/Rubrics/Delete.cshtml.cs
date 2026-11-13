using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Web.Common;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.Rubrics;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DeleteModel(IRubricService rubrics) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        var rubric = await rubrics.GetAsync(id, cancellationToken);
        try
        {
            await rubrics.DeleteAsync(id, cancellationToken);
            TempData.Toast("Đã xóa rubric.");
        }
        catch (ResourceConflictException)
        {
            TempData.Toast("Không thể xóa rubric của câu hỏi đã duyệt. Sửa câu hỏi để đưa về Nháp trước.", success: false);
        }
        return RedirectToPage("/Questions/Details", new { id = rubric.QuestionId });
    }

    public IActionResult OnGet() => NotFound();
}
