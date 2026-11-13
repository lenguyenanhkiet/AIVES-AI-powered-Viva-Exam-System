using AIpoweredVivaExamSystem.Business.Subjects;
using AIpoweredVivaExamSystem.Business.Exams;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Exams;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DetailsModel(ExamService exams, ExamFoundationService foundation, SubjectService subjects) : PageModel
{
    public ExamDetailsViewModel Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var details = await exams.GetAsync(id, ct);
        var counts = await foundation.CountsAsync(id, ct);
        var subject = await subjects.GetAsync(details.Summary.SubjectId, ct);

        Vm = new ExamDetailsViewModel
        {
            Details = details,
            SubjectCode = subject.Code,
            SubjectName = subject.Name,
            Counts = counts
        };
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
