using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.VivaSessions;

[Authorize(Roles = "STUDENT")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IndexModel(AIpoweredVivaExamSystem.Application.VivaSessions.IMyVivaSessionsReader mine) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken ct) => RenderContent("/Pages/VivaSessions/_IndexContent.cshtml", await mine.ListAsync(ct));

    public IActionResult OnPost() => NotFound();
}
