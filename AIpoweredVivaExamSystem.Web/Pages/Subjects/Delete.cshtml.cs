using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.Subjects;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN")]
public sealed class DeleteModel(ISubjectService subjects) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken) =>
        RenderContent("/Pages/Subjects/_DeleteContent.cshtml", await subjects.GetAsync(id, cancellationToken));

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await subjects.DeleteAsync(id, cancellationToken);
            TempData.Toast("Đã xóa môn học.");
            return RedirectToPage("/Subjects/Index");
        }
        catch (ResourceConflictException)
        {
            TempData.Toast("Không thể xóa: môn học vẫn còn chủ đề hoặc câu hỏi. Hãy xóa chúng trước.", success: false);
            return RedirectToPage("/Subjects/Details", new { id });
        }
    }
}
