using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.ExamFoundation;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DetailsModel(ExamAccessGuard guard, AIpoweredVivaExamSystem.Application.VivaSessions.IExamLiveReader live) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        await guard.CanManageExamAsync(id, ct);
        return RenderContent("/Pages/ExamFoundation/_DetailsContent.cshtml", await live.SummaryAsync(id, ct));
    }

    public IActionResult OnPost() => NotFound();
}
