using AIpoweredVivaExamSystem.Business.ExamStudents;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels.ExamStudents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.ExamStudents;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class RosterModel(ExamRosterService roster) : PageModel
{
    public AIpoweredVivaExamSystem.Presentation.ViewModels.ExamStudents.ExamRosterViewModel Vm { get; private set; } = default!;

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
        var exam = await roster.GetExamAsync(examId, ct);
        return new() { Exam = exam, Students = students, Form = form };
    }

    public IActionResult OnPost() => NotFound();
}
