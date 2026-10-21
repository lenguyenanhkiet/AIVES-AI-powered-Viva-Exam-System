using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Web.Pages.Exams;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class TransitionModel(IExamService examService) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, ExamStatus target, string rowVersion, CancellationToken ct)
    {
        try
        {
            byte[] expectedVersion = string.IsNullOrEmpty(rowVersion)
                ? []
                : Convert.FromBase64String(rowVersion);

            await examService.TransitionAsync(id, target, expectedVersion, ct);
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
