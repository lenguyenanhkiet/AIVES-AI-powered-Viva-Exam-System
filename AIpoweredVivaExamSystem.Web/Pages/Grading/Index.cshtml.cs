using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.Grading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AIpoweredVivaExamSystem.Web.Pages.Grading;
[Authorize(Roles = "ADMIN,LECTURER")]
[ResponseCache(NoStore=true, Location=ResponseCacheLocation.None)]
public sealed class IndexModel(GradingService grading, IExamFoundationReader exams) : PageModel
{
    public IReadOnlyList<ExamSummary> Exams { get; private set; } = [];
    public GradingExamList? Vm { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid? examId, CancellationToken ct)
    {
        try {
            Exams = await exams.ListManagedAsync(ct);
            if (examId.HasValue) Vm = await grading.ListAsync(examId.Value,ct);
            return Page();
        } catch (UnauthorizedAccessException) { return StatusCode(403); }
    }
}
