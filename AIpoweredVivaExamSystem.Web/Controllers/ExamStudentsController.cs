using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.ExamStudents;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels.ExamStudents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Web.Controllers;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ExamStudentsController(IExamRosterService roster, IExamRosterReader details) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page, CancellationToken ct) =>
        View(await details.ListManagedAsync(ModelState.IsValid ? page : 1, ct));

    [HttpGet]
    public async Task<IActionResult> Roster(Guid id, CancellationToken ct) => View(await PageAsync(id, new(), ct));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(Guid id, AddRosterStudentViewModel form, CancellationToken ct)
    {
        // Load through the guarded service even for invalid forms: form errors never bypass ownership checks.
        var page = await PageAsync(id, form, ct);
        if (!ModelState.IsValid) return View(nameof(Roster), page);
        try
        {
            await roster.AddAsync(id, form.Email, ct);
            TempData.Toast("Đã thêm sinh viên vào kỳ thi.");
            return RedirectToAction(nameof(Roster), new { id });
        }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            // Refresh SQL after a failed write; keep the submitted email and validation errors.
            return View(nameof(Roster), await PageAsync(id, form, ct));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid id, Guid enrollmentId, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await roster.ListAsync(id, ct);
            return BadRequest();
        }
        try
        {
            await roster.RemoveAsync(id, enrollmentId, ct);
            TempData.Toast("Đã bỏ sinh viên khỏi kỳ thi.");
        }
        catch (ResourceConflictException ex)
        {
            TempData.Toast(ex.Message, success: false);
        }
        return RedirectToAction(nameof(Roster), new { id });
    }

    private async Task<ExamRosterViewModel> PageAsync(Guid examId, AddRosterStudentViewModel form, CancellationToken ct)
    {
        var students = await roster.ListAsync(examId, ct);
        var exam = await details.GetExamAsync(examId, ct);
        return new() { Exam = exam, Students = students, Form = form };
    }
}
