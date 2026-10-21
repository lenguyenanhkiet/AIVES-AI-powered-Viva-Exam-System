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
public sealed class ExamModel(IScheduleReader reader) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) => RenderContent("/Pages/Schedules/_ExamContent.cshtml", await ModelAsync(id, null, ct));

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

    public IActionResult OnPost() => NotFound();
}
