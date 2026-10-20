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

namespace AIpoweredVivaExamSystem.Web.Controllers;

[Authorize]
[Route("Schedules")]
public sealed class SchedulesController(IExamScheduleService schedules, IScheduleReader reader) : Controller
{
    [Authorize(Roles = "ADMIN,LECTURER"), HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View("Index", new ScheduleIndexViewModel { Exams = await reader.ListManagedAsync(ct) });

    [Authorize(Roles = "ADMIN,LECTURER"), HttpGet("Exam/{id:guid}")]
    public async Task<IActionResult> Exam(Guid id, CancellationToken ct) => View("Exam", await ModelAsync(id, null, ct));

    [Authorize(Roles = "ADMIN,LECTURER"), HttpPost("Preview/{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(Guid id, CancellationToken ct)
    {
        SchedulePreview? preview = null;
        try { preview = await schedules.PreviewAsync(id, ct); }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        { TempData.Toast(ex.Message, success: false); }
        return View("Exam", await ModelAsync(id, preview, ct));
    }

    [Authorize(Roles = "ADMIN,LECTURER"), HttpPost("Apply/{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(Guid id, string? previewToken, CancellationToken ct)
    {
        try
        {
            await schedules.ApplyAsync(id, previewToken ?? "", ct);
            TempData.Toast("Đã lưu lịch thi.");
        }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        { TempData.Toast(ex.Message, success: false); }
        return RedirectToAction(nameof(Exam), new { id });
    }

    private async Task<ExamScheduleViewModel> ModelAsync(Guid id, SchedulePreview? preview, CancellationToken ct)
    {
        var current = await reader.GetAsync(id, ct);
        var roster = current.Roster.ToDictionary(x => x.EnrollmentId);
        return new ExamScheduleViewModel
        {
            Exam = current.Exam,
            DurationMinutes = current.DurationMinutes,
            CanEdit = current.Exam.Status == ExamStatus.Draft,
            Current = current.Roster.Select((x, i) => Row(x, i, x.ScheduledAt,
                x.ScheduledAt is { } start ? ExamEntity.SlotEnd(start, current.DurationMinutes) : null)).ToArray(),
            Preview = preview?.Slots.Where(x => roster.ContainsKey(x.EnrollmentId))
                .Select((x, i) => Row(roster[x.EnrollmentId], i, x.StartAt, x.EndAt)).ToArray(),
            PreviewToken = preview?.PreviewToken
        };
    }

    private static ScheduleRow Row(ScheduleEnrollment entry, int index, DateTimeOffset? start, DateTimeOffset? end) =>
        new() { Order = index + 1, EnrollmentId = entry.EnrollmentId, StudentName = entry.StudentName,
            Email = entry.Email, StartAt = start, EndAt = end };
}
