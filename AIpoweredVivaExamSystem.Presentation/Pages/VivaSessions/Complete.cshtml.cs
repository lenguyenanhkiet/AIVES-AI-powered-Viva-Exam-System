using AIpoweredVivaExamSystem.Business.VivaSessions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Presentation.Common;

namespace AIpoweredVivaExamSystem.Presentation.Pages.VivaSessions;

[Authorize(Roles = "STUDENT")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CompleteModel(VivaSessionService sessions) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        await sessions.CompleteMySessionAsync(id, ct);
        TempData["SessionMessage"] = "Đã hoàn thành phiên thi. Bạn có thể mở lại để xem lại các câu hỏi.";
        return RedirectToPage("/VivaSessions/Index");
    }

    public IActionResult OnGet() => NotFound();
}
