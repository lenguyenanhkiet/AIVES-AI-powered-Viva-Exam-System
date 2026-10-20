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

namespace AIpoweredVivaExamSystem.Web.Controllers;

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ExamsController(
    IExamService examService,
    ExamService concreteExamService,
    IExamFoundationReader reader,
    ISubjectService subjectService) : Controller
{
    // GET: Exams?status=
    public async Task<IActionResult> Index(ExamStatus? status, CancellationToken ct)
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

        return View(new ExamIndexViewModel
        {
            Exams = filtered,
            SubjectNames = subjectMap,
            SubjectCodes = codeMap,
            StatusCounts = statusCounts,
            TotalCount = all.Count,
            FilterStatus = status
        });
    }

    // GET: Exams/Details/{id}
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var details = await examService.GetAsync(id, ct);
        var counts = await reader.CountsAsync(id, ct);
        var subject = await subjectService.GetAsync(details.Summary.SubjectId, ct);

        return View(new ExamDetailsViewModel
        {
            Details = details,
            SubjectCode = subject.Code,
            SubjectName = subject.Name,
            Counts = counts
        });
    }

    // GET: Exams/Create
    public async Task<IActionResult> Create(Guid? subjectId, CancellationToken ct)
    {
        var subjects = await GetSubjectOptionsAsync(ct);
        return View(new ExamFormViewModel
        {
            SubjectId = subjectId ?? Guid.Empty,
            SubjectOptions = subjects
        });
    }

    // POST: Exams/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ExamFormViewModel model, CancellationToken ct)
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
                return RedirectToAction(nameof(Details), new { id = created.Summary.Id });
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
        return View(model);
    }

    // GET: Exams/Edit/{id}
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var details = await examService.GetAsync(id, ct);
        if (details.Summary.Status != ExamStatus.Draft)
        {
            TempData.Toast("Chỉ có thể chỉnh sửa kỳ thi ở trạng thái Bản nháp.", false);
            return RedirectToAction(nameof(Details), new { id });
        }

        var subjects = await GetSubjectOptionsAsync(ct);
        return View(ExamFormViewModel.From(details, subjects));
    }

    // POST: Exams/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ExamFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (ModelState.IsValid)
        {
            try
            {
                byte[] expectedVersion = string.IsNullOrEmpty(model.RowVersionBase64)
                    ? []
                    : Convert.FromBase64String(model.RowVersionBase64);

                await examService.UpdateAsync(id, new ExamConfiguration(
                    model.SubjectId,
                    model.Name,
                    new DateTimeOffset(model.StartAt, TimeSpan.FromHours(7)),
                    new DateTimeOffset(model.EndAt, TimeSpan.FromHours(7)),
                    model.DurationMinutes,
                    model.NumberOfMainQuestions,
                    model.MaxFollowUpPerQuestion), expectedVersion, ct);

                TempData.Toast("Đã lưu thay đổi kỳ thi.");
                return RedirectToAction(nameof(Details), new { id });
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
        return View(model);
    }

    // POST: Exams/Transition/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(Guid id, ExamStatus target, string rowVersion, CancellationToken ct)
    {
        try
        {
            byte[] expectedVersion = string.IsNullOrEmpty(rowVersion)
                ? []
                : Convert.FromBase64String(rowVersion);

            await examService.TransitionAsync(id, target, expectedVersion, ct);
            TempData.Toast($"Đã chuyển trạng thái kỳ thi sang \"{target.ToLabel()}\".");
        }
        catch (Exception ex)
        {
            TempData.Toast(ex.Message, false);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: Exams/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, string rowVersion, CancellationToken ct)
    {
        try
        {
            byte[] expectedVersion = string.IsNullOrEmpty(rowVersion)
                ? []
                : Convert.FromBase64String(rowVersion);

            await concreteExamService.DeleteDraftAsync(id, expectedVersion, ct);
            TempData.Toast("Đã xóa kỳ thi.");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData.Toast(ex.Message, false);
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private async Task<IReadOnlyList<SelectListItem>> GetSubjectOptionsAsync(CancellationToken ct)
    {
        var subjects = await subjectService.ListAsync(new SubjectListQuery { PageSize = 100 }, ct);
        return subjects.Items
            .Select(s => new SelectListItem($"{s.Code} — {s.Name}", s.Id.ToString()))
            .ToList();
    }
}
