using AIpoweredVivaExamSystem.Business.Questions;
using AIpoweredVivaExamSystem.DataAccess.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Questions;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ApproveModel(QuestionService questions) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await questions.ApproveAsync(id, cancellationToken);
            TempData.Toast("Đã duyệt câu hỏi. Câu hỏi sẵn sàng dùng cho kỳ thi.");
        }
        catch (ResourceConflictException)
        {
            TempData.Toast("Chưa thể duyệt: câu hỏi cần có rubric chấm điểm trước.", success: false);
        }
        catch (DomainValidationException)
        {
            TempData.Toast("Chỉ duyệt được câu hỏi đang ở trạng thái Nháp.", success: false);
        }
        return RedirectToPage("/Questions/Details", new { id });
    }

    public IActionResult OnGet() => NotFound();
}
