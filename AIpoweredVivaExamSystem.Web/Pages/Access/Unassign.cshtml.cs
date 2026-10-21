using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.Access;

[Authorize(Roles = "ADMIN")]
public sealed class UnassignModel(AccessService access) : ContentPageModel
{
    public Task<IActionResult> OnPost(Guid id, CancellationToken ct) => ExecuteAsync(() => access.UnassignAsync(id, ct));

    private async Task<IActionResult> ExecuteAsync(Func<Task> action)
    {
        try { await action(); TempData["Toast"] = "Đã cập nhật phân quyền."; }
        catch (ResourceConflictException ex) { TempData["Toast"] = ex.Message; TempData["ToastType"] = "error"; }
        return RedirectToPage("/Access/Index");
    }

    public IActionResult OnGet() => NotFound();
}
