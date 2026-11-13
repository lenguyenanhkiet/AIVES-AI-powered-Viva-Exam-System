using AIpoweredVivaExamSystem.Business.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Presentation.Common;

namespace AIpoweredVivaExamSystem.Presentation.Pages.Access;

[Authorize(Roles = "ADMIN")]
public sealed class AssignModel(AccessService access) : PageModel
{
    public Task<IActionResult> OnPost(Guid lecturerId, Guid subjectId, CancellationToken ct) => ExecuteAsync(() => access.AssignSubjectAsync(lecturerId, subjectId, ct));

    private async Task<IActionResult> ExecuteAsync(Func<Task> action)
    {
        try { await action(); TempData["Toast"] = "Đã cập nhật phân quyền."; }
        catch (ResourceConflictException ex) { TempData["Toast"] = ex.Message; TempData["ToastType"] = "error"; }
        return RedirectToPage("/Access/Index");
    }

    public IActionResult OnGet() => NotFound();
}
