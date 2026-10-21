using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Web.Pages.Questions;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class RejectModel(IQuestionService questions) : PageModel
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
