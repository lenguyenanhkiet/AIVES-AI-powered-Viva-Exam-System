using AIpoweredVivaExamSystem.Business.ExamStudents;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels.ExamStudents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.ExamStudents;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class RemoveModel(ExamRosterService roster) : PageModel
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
