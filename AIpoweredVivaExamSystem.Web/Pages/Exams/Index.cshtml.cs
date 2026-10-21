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
public sealed class IndexModel(ExamService concreteExamService, ISubjectService subjectService) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(ExamStatus? status, CancellationToken ct)
    {
        var all = await concreteExamService.ListManagedAsync(ct);
        var subjects = await subjectService.ListAsync(new SubjectListQuery { PageSize = 100 }, ct);
        var subjectMap = subjects.Items.ToDictionary(s => s.Id, s => s.Name);
        var codeMap = subjects.Items.ToDictionary(s => s.Id, s => s.Code);

        var statusCounts = new Dictionary<ExamStatus, int>();
        foreach (var st in Enum.GetValues<ExamStatus>())
        {
            statusCounts[st] = all.Count(x => x.Status == st);
        }

        var filtered = status.HasValue ? all.Where(x => x.Status == status.Value).ToList() : all;

        return RenderContent("/Pages/Exams/_IndexContent.cshtml", new ExamIndexViewModel
        {
            Exams = filtered,
            SubjectNames = subjectMap,
            SubjectCodes = codeMap,
            StatusCounts = statusCounts,
            TotalCount = all.Count,
            FilterStatus = status
        });
    }

    public IActionResult OnPost() => NotFound();
}
