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
public sealed class DetailsModel(IExamService examService, IExamFoundationReader reader, ISubjectService subjectService) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var details = await examService.GetAsync(id, ct);
        var counts = await reader.CountsAsync(id, ct);
        var subject = await subjectService.GetAsync(details.Summary.SubjectId, ct);

        return RenderContent("/Pages/Exams/_DetailsContent.cshtml", new ExamDetailsViewModel
        {
            Details = details,
            SubjectCode = subject.Code,
            SubjectName = subject.Name,
            Counts = counts
        });
    }

    public IActionResult OnPost() => NotFound();
}
