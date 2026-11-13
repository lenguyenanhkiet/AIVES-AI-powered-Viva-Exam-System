using AIpoweredVivaExamSystem.Business.Scheduling;
using AIpoweredVivaExamSystem.DataAccess.Common;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExamEntity = AIpoweredVivaExamSystem.DataAccess.Entities.Exam;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Schedules;

[Authorize]
[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ApplyModel(ExamScheduleService schedules) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, string? previewToken, CancellationToken ct)
    {
        try
        {
            await schedules.ApplyAsync(id, previewToken ?? "", ct);
            TempData.Toast("Đã lưu lịch thi.");
        }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        { TempData.Toast(ex.Message, success: false); }
        return RedirectToPage("/Schedules/Exam", new { id });
    }

    public IActionResult OnGet() => NotFound();
}
