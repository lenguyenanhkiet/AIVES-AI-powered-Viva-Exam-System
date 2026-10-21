using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.ExamStudents;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels.ExamStudents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.ExamStudents;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class RosterModel(IExamRosterService roster, IExamRosterReader details) : PageModel
{
    public AIpoweredVivaExamSystem.Web.ViewModels.ExamStudents.ExamRosterViewModel Vm { get; private set; } = default!;

    [BindProperty]
    public AddRosterStudentViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Vm = await PageAsync(id, new(), ct);
        return Page();
    }

    private async Task<ExamRosterViewModel> PageAsync(Guid examId, AddRosterStudentViewModel form, CancellationToken ct)
    {
        var students = await roster.ListAsync(examId, ct);
        var exam = await details.GetExamAsync(examId, ct);
        return new() { Exam = exam, Students = students, Form = form };
    }

    public IActionResult OnPost() => NotFound();
}
