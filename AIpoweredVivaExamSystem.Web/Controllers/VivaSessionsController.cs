using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Web.Controllers;

[Authorize(Roles = "STUDENT")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class VivaSessionsController(IVivaSessionService sessions,
    AIpoweredVivaExamSystem.Application.VivaSessions.IMyVivaSessionsReader mine) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await mine.ListAsync(ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(Guid enrollmentId, CancellationToken ct)
    {
        try { return View("Session", await sessions.GetOrCreateMySessionAsync(enrollmentId, ct)); }
        catch (DomainValidationException ex) { ModelState.AddModelError("", ex.Message); return View("Index", await mine.ListAsync(ct)); }
        catch (ResourceConflictException ex) { ModelState.AddModelError("", ex.Message); return View("Index", await mine.ListAsync(ct)); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await sessions.CompleteMySessionAsync(id, ct);
        TempData["SessionMessage"] = "Đã hoàn thành phiên thi. Bạn có thể mở lại để xem lại các câu hỏi.";
        return RedirectToAction(nameof(Index));
    }
}
