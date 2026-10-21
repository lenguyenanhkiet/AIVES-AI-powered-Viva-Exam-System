using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Web.Pages.Exams;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class DetailsModel(IExamService examService, IExamFoundationReader reader, ISubjectService subjectService) : PageModel
{
    public ExamDetailsViewModel Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var details = await examService.GetAsync(id, ct);
        var counts = await reader.CountsAsync(id, ct);
        var subject = await subjectService.GetAsync(details.Summary.SubjectId, ct);

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
