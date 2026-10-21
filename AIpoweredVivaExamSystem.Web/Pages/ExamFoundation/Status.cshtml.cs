using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.ExamFoundation;

[Authorize(Roles = "ADMIN,LECTURER")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StatusModel(ExamAccessGuard guard, AIpoweredVivaExamSystem.Application.VivaSessions.IExamLiveReader live) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        await guard.CanManageExamAsync(id, ct);
        var status = await live.ReadAsync(id, ct);
        return new JsonResult(new
        {
            Status = status.Status.ToLabel(), status.Counts, status.CompletedSessions,
            Roster = status.Roster.Select(row => new
            {
                row.EnrollmentId, row.Name, row.ScheduledAt,
                SessionStatus = row.SessionStatus.ToLabel()
            })
        });
    }

    public IActionResult OnPost() => NotFound();
}
