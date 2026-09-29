using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.Models;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Web.Controllers;

public sealed class SubjectsController(ISubjectService subjects, ITopicService topics, IQuestionService questions) : Controller
{
    // GET: Subjects?keyword=&status=&page=
    public async Task<IActionResult> Index(SubjectListQuery query, CancellationToken cancellationToken)
    {
        // Invalid filters in a hand-edited URL fall back to the default list instead of an error page.
        if (!ModelState.IsValid || query.Page < 1 || query.PageSize is < 1 or > 100
            || (query.Status is { } status && !Enum.IsDefined(status)))
            query = new SubjectListQuery();
        var page = await subjects.ListAsync(query, cancellationToken);
        return View(new SubjectIndexViewModel { Page = page, Query = query });
    }

    // GET: Subjects/Details/{id}
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var subject = await subjects.GetAsync(id, cancellationToken);
        var subjectTopics = await topics.ListAsync(new TopicListQuery { SubjectId = id, PageSize = 100 }, cancellationToken);
        var questionCount = (await questions.ListAsync(new QuestionListQuery { SubjectId = id, PageSize = 1 }, cancellationToken)).TotalCount;
        return View(new SubjectDetailsViewModel { Subject = subject, Topics = subjectTopics, QuestionCount = questionCount });
    }

    // GET: Subjects/Create
    public IActionResult Create() => View(new SubjectFormViewModel());

    // POST: Subjects/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubjectFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);
        try
        {
            var created = await subjects.CreateAsync(
                new CreateSubjectRequest(model.Code, model.Name, model.Description, model.Status), cancellationToken);
            TempData.Toast($"Đã tạo môn học {created.Code}.");
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }
        catch (Exception exception) when (TryAddError(exception))
        {
            return View(model);
        }
    }

    // GET: Subjects/Edit/{id}
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken) =>
        View(SubjectFormViewModel.From(await subjects.GetAsync(id, cancellationToken)));

    // POST: Subjects/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, SubjectFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!ModelState.IsValid)
            return View(model);
        try
        {
            await subjects.UpdateAsync(id,
                new UpdateSubjectRequest(model.Code, model.Name, model.Description, model.Status), cancellationToken);
            TempData.Toast("Đã lưu thay đổi.");
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception exception) when (TryAddError(exception))
        {
            return View(model);
        }
    }

    // GET: Subjects/Delete/{id}
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        View(await subjects.GetAsync(id, cancellationToken));

    // POST: Subjects/Delete/{id}
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await subjects.DeleteAsync(id, cancellationToken);
            TempData.Toast("Đã xóa môn học.");
            return RedirectToAction(nameof(Index));
        }
        catch (ResourceConflictException)
        {
            TempData.Toast("Không thể xóa: môn học vẫn còn chủ đề hoặc câu hỏi. Hãy xóa chúng trước.", success: false);
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    private bool TryAddError(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validation:
                ModelState.AddErrors(validation);
                return true;
            case ResourceConflictException:
                ModelState.AddModelError(nameof(SubjectFormViewModel.Code), "Mã môn này đã tồn tại.");
                return true;
            case DomainValidationException domain:
                ModelState.AddModelError(string.Empty, domain.Message);
                return true;
            default:
                return false;
        }
    }
}
