using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.ExamStudents;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels.ExamStudents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.ExamStudents;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class RemoveModel(IExamRosterService roster) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, Guid enrollmentId, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await roster.ListAsync(id, ct);
            return BadRequest();
        }
        try
        {
            await roster.RemoveAsync(id, enrollmentId, ct);
            TempData.Toast("Đã bỏ sinh viên khỏi kỳ thi.");
        }
        catch (ResourceConflictException ex)
        {
            TempData.Toast(ex.Message, success: false);
        }
        return RedirectToPage("/ExamStudents/Roster", new { id });
    }

    public IActionResult OnGet() => NotFound();
}
