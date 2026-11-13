using AIpoweredVivaExamSystem.Business.VivaSessions;
using AIpoweredVivaExamSystem.Business.Exams;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.ExamFoundation;

[Authorize(Roles = "ADMIN,LECTURER")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StatusModel(ExamAccessGuard examGuard, ExamLiveService liveExams) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        await examGuard.CanManageExamAsync(id, ct);
        var status = await liveExams.ReadAsync(id, ct);
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
