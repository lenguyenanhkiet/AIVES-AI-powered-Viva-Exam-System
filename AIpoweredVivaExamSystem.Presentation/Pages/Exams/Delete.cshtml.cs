using AIpoweredVivaExamSystem.Business.Exams;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Exams;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DeleteModel(ExamService exams) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, string rowVersion, CancellationToken ct)
    {
        try
        {
            byte[] expectedVersion = string.IsNullOrEmpty(rowVersion)
                ? []
                : Convert.FromBase64String(rowVersion);

            await exams.DeleteDraftAsync(id, expectedVersion, ct);
            TempData.Toast("Đã xóa kỳ thi.");
            return RedirectToPage("/Exams/Index");
        }
        catch (Exception ex)
        {
            TempData.Toast(ex.Message, false);
            return RedirectToPage("/Exams/Details", new { id });
        }
    }

    public IActionResult OnGet() => NotFound();
}
