using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.VivaSessions;

[Authorize(Roles = "STUDENT")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OpenModel(IVivaSessionService sessions, AIpoweredVivaExamSystem.Application.VivaSessions.IMyVivaSessionsReader mine) : ContentPageModel
{
    public async Task<IActionResult> OnPostAsync(Guid enrollmentId, CancellationToken ct)
    {
        try { return RenderContent("/Pages/VivaSessions/_SessionContent.cshtml", await sessions.GetOrCreateMySessionAsync(enrollmentId, ct)); }
        catch (DomainValidationException ex) { ModelState.AddModelError("", ex.Message); return RenderContent("/Pages/VivaSessions/_IndexContent.cshtml", await mine.ListAsync(ct)); }
        catch (ResourceConflictException ex) { ModelState.AddModelError("", ex.Message); return RenderContent("/Pages/VivaSessions/_IndexContent.cshtml", await mine.ListAsync(ct)); }
    }

    public IActionResult OnGet() => NotFound();
}
