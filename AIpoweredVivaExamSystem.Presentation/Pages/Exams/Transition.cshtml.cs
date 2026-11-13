using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Exams;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class TransitionModel(ExamService exams) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, ExamStatus target, string rowVersion, CancellationToken ct)
    {
        try
        {
            byte[] expectedVersion = string.IsNullOrEmpty(rowVersion)
                ? []
                : Convert.FromBase64String(rowVersion);

            await exams.TransitionAsync(id, target, expectedVersion, ct);
            TempData.Toast($"Đã chuyển trạng thái kỳ thi sang \"{target.ToLabel()}\".");
        }
        catch (Exception ex)
        {
            TempData.Toast(ex.Message, false);
        }

        return RedirectToPage("/Exams/Details", new { id });
    }

    public IActionResult OnGet() => NotFound();
}
