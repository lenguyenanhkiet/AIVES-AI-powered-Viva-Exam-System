using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.VivaSessions;

[Authorize(Roles = "STUDENT")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OpenModel(IVivaSessionService sessions, AIpoweredVivaExamSystem.Application.VivaSessions.IMyVivaSessionsReader mine) : PageModel
{
    public AIpoweredVivaExamSystem.Application.Exams.StudentSession? Session { get; private set; }

    public IReadOnlyList<AIpoweredVivaExamSystem.Application.VivaSessions.MyVivaExam> Vm { get; private set; } = default!;

    public async Task<IActionResult> OnPostAsync(Guid enrollmentId, CancellationToken ct)
    {
        try
        {
            Session = await sessions.GetOrCreateMySessionAsync(enrollmentId, ct);
        }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        {
            ModelState.AddModelError("", ex.Message);
            Vm = await mine.ListAsync(ct);
        }
        return Page();
    }

    public IActionResult OnGet() => NotFound();
}
