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
public sealed class ApproveModel(IQuestionService questions) : ContentPageModel
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
