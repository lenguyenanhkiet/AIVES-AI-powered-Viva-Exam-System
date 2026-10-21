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
public sealed class AddModel(IExamRosterService roster, IExamRosterReader details) : PageModel
{
    public AIpoweredVivaExamSystem.Web.ViewModels.ExamStudents.ExamRosterViewModel Vm { get; private set; } = default!;

    [BindProperty]
    public AddRosterStudentViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        // Load through the guarded service even for invalid forms: Input errors never bypass ownership checks.
        var page = await PageAsync(id, Input, ct);
        if (!ModelState.IsValid)
        {
            Vm = page;
            return Page();
        }
        try
        {
            await roster.AddAsync(id, Input.Email, ct);
            TempData.Toast("Đã thêm sinh viên vào kỳ thi.");
            return RedirectToPage("/ExamStudents/Roster", new { id });
        }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            // Refresh SQL after a failed write; keep the submitted email and validation errors.
            Vm = await PageAsync(id, Input, ct);
            return Page();
        }
    }

    private async Task<ExamRosterViewModel> PageAsync(Guid examId, AddRosterStudentViewModel form, CancellationToken ct)
    {
        var students = await roster.ListAsync(examId, ct);
        var exam = await details.GetExamAsync(examId, ct);
        return new() { Exam = exam, Students = students, Form = form };
    }

    public IActionResult OnGet() => NotFound();
}
