using AIpoweredVivaExamSystem.Business.Subjects;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Subjects;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Subjects;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN")]
public sealed class DeleteModel(SubjectService subjects) : PageModel
{
    public SubjectResponse Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        Vm = await subjects.GetAsync(id, cancellationToken);
        return Page();
    }

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
