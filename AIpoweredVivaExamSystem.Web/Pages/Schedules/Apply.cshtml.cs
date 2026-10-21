using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.Scheduling;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExamEntity = AIpoweredVivaExamSystem.Domain.Entities.Exam;


namespace AIpoweredVivaExamSystem.Web.Pages.Schedules;

[Authorize]
[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ApplyModel(IExamScheduleService schedules) : ContentPageModel
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
