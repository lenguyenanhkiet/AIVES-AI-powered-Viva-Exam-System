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
public sealed class CreateModel(IExamService examService, ISubjectService subjectService) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid? subjectId, CancellationToken ct)
    {
        var subjects = await GetSubjectOptionsAsync(ct);
        return RenderContent("/Pages/Exams/_CreateContent.cshtml", new ExamFormViewModel
        {
            SubjectId = subjectId ?? Guid.Empty,
            SubjectOptions = subjects
        });
    }

    public async Task<IActionResult> OnPostAsync(ExamFormViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await examService.CreateAsync(new ExamConfiguration(
                    model.SubjectId,
                    model.Name,
                    new DateTimeOffset(model.StartAt, TimeSpan.FromHours(7)),
                    new DateTimeOffset(model.EndAt, TimeSpan.FromHours(7)),
                    model.DurationMinutes,
                    model.NumberOfMainQuestions,
                    model.MaxFollowUpPerQuestion), ct);

                TempData.Toast("Đã tạo kỳ thi thành công.");
                return RedirectToPage("/Exams/Details", new { id = created.Summary.Id });
            }
            catch (DomainValidationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (ResourceConflictException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        model.SubjectOptions = await GetSubjectOptionsAsync(ct);
        return RenderContent("/Pages/Exams/_CreateContent.cshtml", model);
    }

    private async Task<IReadOnlyList<SelectListItem>> GetSubjectOptionsAsync(CancellationToken ct)
    {
        var subjects = await subjectService.ListAsync(new SubjectListQuery { PageSize = 100 }, ct);
        return subjects.Items
            .Select(s => new SelectListItem($"{s.Code} — {s.Name}", s.Id.ToString()))
            .ToList();
    }
}
