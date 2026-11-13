using AIpoweredVivaExamSystem.Business.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;
using AIpoweredVivaExamSystem.Presentation.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIpoweredVivaExamSystem.Presentation.Pages.VivaSessions;

[Authorize(Roles = "STUDENT")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OpenModel(VivaInterviewService interview, MyVivaSessionsService mySessions) : PageModel
{
    public StudentSession? Session { get; private set; }
    public VivaInterviewState? InterviewState { get; private set; }
    public IReadOnlyList<MyVivaExam> Vm { get; private set; } = default!;

    public async Task<IActionResult> OnPostAsync(Guid enrollmentId, CancellationToken ct)
    {
        try
        {
            InterviewState = await interview.OpenOrResumeByEnrollmentAsync(enrollmentId, ct);
            Session = new StudentSession(
                InterviewState.SessionId,
                InterviewState.StartedAt,
                InterviewState.CompletedAt,
                InterviewState.Questions);
        }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        {
            ModelState.AddModelError("", ex.Message);
            Vm = await mySessions.ListAsync(ct);
        }
        return Page();
    }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken ct)
    {
        if (id is not { } sessionId || sessionId == Guid.Empty)
            return NotFound();

        try
        {
            InterviewState = await interview.GetStateAsync(sessionId, ct);
            Session = new StudentSession(
                InterviewState.SessionId,
                InterviewState.StartedAt,
                InterviewState.CompletedAt,
                InterviewState.Questions);
            return Page();
        }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        {
            ModelState.AddModelError("", ex.Message);
            Vm = await mySessions.ListAsync(ct);
            return Page();
        }
    }
}
