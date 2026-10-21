using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.VivaSessions;

[Authorize(Roles = "STUDENT")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CompleteModel(IVivaSessionService sessions) : ContentPageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        await sessions.CompleteMySessionAsync(id, ct);
        TempData["SessionMessage"] = "Đã hoàn thành phiên thi. Bạn có thể mở lại để xem lại các câu hỏi.";
        return RedirectToPage("/VivaSessions/Index");
    }

    public IActionResult OnGet() => NotFound();
}
