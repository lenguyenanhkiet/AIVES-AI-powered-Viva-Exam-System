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
public sealed class AddModel(IExamRosterService roster, IExamRosterReader details) : ContentPageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, AddRosterStudentViewModel form, CancellationToken ct)
    {
        // Load through the guarded service even for invalid forms: form errors never bypass ownership checks.
        var page = await PageAsync(id, form, ct);
        if (!ModelState.IsValid) return RenderContent("/Pages/ExamStudents/_RosterContent.cshtml", page);
        try
        {
            await roster.AddAsync(id, form.Email, ct);
            TempData.Toast("Đã thêm sinh viên vào kỳ thi.");
            return RedirectToPage("/ExamStudents/Roster", new { id });
        }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            // Refresh SQL after a failed write; keep the submitted email and validation errors.
            return RenderContent("/Pages/ExamStudents/_RosterContent.cshtml", await PageAsync(id, form, ct));
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
