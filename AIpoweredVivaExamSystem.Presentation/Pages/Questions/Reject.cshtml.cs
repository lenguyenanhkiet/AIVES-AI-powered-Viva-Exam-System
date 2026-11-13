using AIpoweredVivaExamSystem.Business.Questions;
using AIpoweredVivaExamSystem.DataAccess.Common;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Questions;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class RejectModel(QuestionService questions) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await questions.RejectAsync(id, cancellationToken);
            TempData.Toast("Đã từ chối câu hỏi. Sửa lại nội dung để gửi duyệt lần nữa.");
        }
        catch (DomainValidationException)
        {
            TempData.Toast("Chỉ từ chối được câu hỏi đang ở trạng thái Nháp.", success: false);
        }
        return RedirectToPage("/Questions/Details", new { id });
    }

    public IActionResult OnGet() => NotFound();
}
