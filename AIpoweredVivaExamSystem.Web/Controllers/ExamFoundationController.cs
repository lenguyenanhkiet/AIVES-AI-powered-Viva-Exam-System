using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Web.Controllers;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ExamFoundationController(ExamFoundationService foundation, ExamAccessGuard guard,
    AIpoweredVivaExamSystem.Application.VivaSessions.IExamLiveReader live) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await foundation.ListAsync(ct));
    [HttpGet]
    public async Task<IActionResult> Counts(Guid id, CancellationToken ct) => Json(await foundation.CountsAsync(id, ct));
    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        await guard.CanManageExamAsync(id, ct);
        return View(await live.SummaryAsync(id, ct));
    }
    [HttpGet, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Status(Guid id, CancellationToken ct)
    {
        await guard.CanManageExamAsync(id, ct);
        var status = await live.ReadAsync(id, ct);
        return Json(new
        {
            Status = status.Status.ToLabel(), status.Counts, status.CompletedSessions,
            Roster = status.Roster.Select(row => new
            {
                row.EnrollmentId, row.Name, row.ScheduledAt,
                SessionStatus = row.SessionStatus.ToLabel()
            })
        });
    }
}
