using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.ExamFoundation;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class CountsModel(ExamFoundationService foundation) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) => new JsonResult(await foundation.CountsAsync(id, ct));

    public IActionResult OnPost() => NotFound();
}
