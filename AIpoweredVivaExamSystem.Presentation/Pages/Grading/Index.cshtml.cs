using AIpoweredVivaExamSystem.Business.Grading;
using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Grading;
using AIpoweredVivaExamSystem.Presentation.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AIpoweredVivaExamSystem.Presentation.Pages.Grading;
[Authorize(Roles = "ADMIN,LECTURER")]
[ResponseCache(NoStore=true, Location=ResponseCacheLocation.None)]
public sealed class IndexModel(ExamFoundationService foundation, GradingService grading) : PageModel
{
    public IReadOnlyList<ExamSummary> Exams { get; private set; } = [];
    public GradingExamList? Vm { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid? examId, CancellationToken ct)
    {
        try {
            Exams = await foundation.ListAsync(ct);
            if (examId.HasValue) Vm = await grading.ListAsync(examId.Value,ct);
            return Page();
        } catch (UnauthorizedAccessException) { return StatusCode(403); }
    }
}

