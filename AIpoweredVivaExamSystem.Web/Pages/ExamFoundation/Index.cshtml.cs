using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.ExamFoundation;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel(ExamFoundationService foundation) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken ct) => RenderContent("/Pages/ExamFoundation/_IndexContent.cshtml", await foundation.ListAsync(ct));

    public IActionResult OnPost() => NotFound();
}
