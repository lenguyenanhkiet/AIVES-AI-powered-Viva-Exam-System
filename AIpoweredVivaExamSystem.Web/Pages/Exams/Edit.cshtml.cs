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
public sealed class EditModel(IExamService examService, ISubjectService subjectService) : PageModel
{
    [BindProperty]
    public ExamFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var details = await examService.GetAsync(id, ct);
        if (details.Summary.Status != ExamStatus.Draft)
        {
            TempData.Toast("Chỉ có thể chỉnh sửa kỳ thi ở trạng thái Bản nháp.", false);
            return RedirectToPage("/Exams/Details", new { id });
        }

        var subjects = await GetSubjectOptionsAsync(ct);
        Input = ExamFormViewModel.From(details, subjects);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        Input.Id = id;
        if (ModelState.IsValid)
        {
            try
            {
                byte[] expectedVersion = string.IsNullOrEmpty(Input.RowVersionBase64)
                    ? []
                    : Convert.FromBase64String(Input.RowVersionBase64);

                await examService.UpdateAsync(id, new ExamConfiguration(
                    Input.SubjectId,
                    Input.Name,
                    new DateTimeOffset(Input.StartAt, TimeSpan.FromHours(7)),
                    new DateTimeOffset(Input.EndAt, TimeSpan.FromHours(7)),
                    Input.DurationMinutes,
                    Input.NumberOfMainQuestions,
                    Input.MaxFollowUpPerQuestion), expectedVersion, ct);

                TempData.Toast("Đã lưu thay đổi kỳ thi.");
                return RedirectToPage("/Exams/Details", new { id });
            }
            catch (DomainValidationException ex)
            {
                ModelState.AddModelError(nameof(Input), ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                ModelState.AddModelError(nameof(Input), ex.Message);
            }
            catch (ResourceConflictException ex)
            {
                ModelState.AddModelError(nameof(Input), ex.Message);
            }
        }

        Input.SubjectOptions = await GetSubjectOptionsAsync(ct);
        return Page();
    }

    private async Task<IReadOnlyList<SelectListItem>> GetSubjectOptionsAsync(CancellationToken ct)
    {
        var subjects = await subjectService.ListAsync(new SubjectListQuery { PageSize = 100 }, ct);
        return subjects.Items
            .Select(s => new SelectListItem($"{s.Code} — {s.Name}", s.Id.ToString()))
            .ToList();
    }
}
