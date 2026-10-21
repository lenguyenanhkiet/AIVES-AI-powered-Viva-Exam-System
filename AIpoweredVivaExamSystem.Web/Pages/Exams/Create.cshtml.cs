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
public sealed class CreateModel(IExamService examService, ISubjectService subjectService) : PageModel
{
    [BindProperty]
    public ExamFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid? subjectId, CancellationToken ct)
    {
        var subjects = await GetSubjectOptionsAsync(ct);
        Input = new ExamFormViewModel
        {
            SubjectId = subjectId ?? Guid.Empty,
            SubjectOptions = subjects
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await examService.CreateAsync(new ExamConfiguration(
                    Input.SubjectId,
                    Input.Name,
                    new DateTimeOffset(Input.StartAt, TimeSpan.FromHours(7)),
                    new DateTimeOffset(Input.EndAt, TimeSpan.FromHours(7)),
                    Input.DurationMinutes,
                    Input.NumberOfMainQuestions,
                    Input.MaxFollowUpPerQuestion), ct);

                TempData.Toast("Đã tạo kỳ thi thành công.");
                return RedirectToPage("/Exams/Details", new { id = created.Summary.Id });
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
