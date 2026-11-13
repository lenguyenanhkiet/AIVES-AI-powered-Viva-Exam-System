using AIpoweredVivaExamSystem.Business.Subjects;
using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Models.Subjects;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Exams;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class IndexModel(ExamService exams, SubjectService subjectService) : PageModel
{
    public ExamIndexViewModel Vm { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(ExamStatus? status, CancellationToken ct)
    {
        var all = await exams.ListManagedAsync(ct);
        var subjects = await subjectService.ListAsync(new SubjectListQuery { PageSize = 100 }, ct);
        var subjectMap = subjects.Items.ToDictionary(s => s.Id, s => s.Name);
        var codeMap = subjects.Items.ToDictionary(s => s.Id, s => s.Code);

        var statusCounts = new Dictionary<ExamStatus, int>();
        foreach (var st in Enum.GetValues<ExamStatus>())
        {
            statusCounts[st] = all.Count(x => x.Status == st);
        }

        var filtered = status.HasValue ? all.Where(x => x.Status == status.Value).ToList() : all;

        Vm = new ExamIndexViewModel
        {
            Exams = filtered,
            SubjectNames = subjectMap,
            SubjectCodes = codeMap,
            StatusCounts = statusCounts,
            TotalCount = all.Count,
            FilterStatus = status
        };
        return Page();
    }

    public IActionResult OnPost() => NotFound();
}
