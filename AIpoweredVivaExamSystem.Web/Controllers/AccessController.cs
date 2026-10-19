using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Web.Controllers;

[Authorize(Roles = "ADMIN")]
public sealed class AccessController(AccessService access) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await access.OverviewAsync(ct));
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Role(Guid userId, UserRole role, CancellationToken ct)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(role)) return BadRequest();
        return await ExecuteAsync(() => access.AssignRoleAsync(userId, role, ct));
    }
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Assign(Guid lecturerId, Guid subjectId, CancellationToken ct) => ExecuteAsync(() => access.AssignSubjectAsync(lecturerId, subjectId, ct));
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Unassign(Guid id, CancellationToken ct) => ExecuteAsync(() => access.UnassignAsync(id, ct));
    private async Task<IActionResult> ExecuteAsync(Func<Task> action)
    {
        try { await action(); TempData["Toast"] = "Đã cập nhật phân quyền."; }
        catch (ResourceConflictException ex) { TempData["Toast"] = ex.Message; TempData["ToastType"] = "error"; }
        return RedirectToAction(nameof(Index));
    }
}
