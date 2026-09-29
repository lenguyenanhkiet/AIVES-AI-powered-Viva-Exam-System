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

public sealed class TopicsController(ITopicService topics, ISubjectService subjects, IQuestionService questions) : Controller
{
    // GET: Topics?subjectId=&keyword=&status=&page=
    public async Task<IActionResult> Index(TopicListQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || query.SubjectId == Guid.Empty || query.Page < 1 || query.PageSize is < 1 or > 100
            || (query.Status is { } status && !Enum.IsDefined(status)))
            query = new TopicListQuery();
        var page = await topics.ListAsync(query, cancellationToken);
        return View(new TopicIndexViewModel
        {
            Page = page, Query = query, Subjects = await SubjectMapAsync(cancellationToken)
        });
    }

    // GET: Topics/Details/{id}
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var topic = await topics.GetAsync(id, cancellationToken);
        (await SubjectMapAsync(cancellationToken)).TryGetValue(topic.SubjectId, out var subject);
        var topicQuestions = await questions.ListAsync(new QuestionListQuery { TopicId = id, PageSize = 10 }, cancellationToken);
        return View(new TopicDetailsViewModel { Topic = topic, Subject = subject, Questions = topicQuestions });
    }

    // GET: Topics/Create?subjectId=
    public async Task<IActionResult> Create(Guid? subjectId, CancellationToken cancellationToken)
    {
        await LoadSubjectsAsync(cancellationToken);
        return View(new TopicFormViewModel { SubjectId = subjectId });
    }

    // POST: Topics/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TopicFormViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await topics.CreateAsync(
                    new CreateTopicRequest(model.SubjectId!.Value, model.Name, model.Description, model.Status),
                    cancellationToken);
                TempData.Toast($"Đã tạo chủ đề \"{created.Name}\".");
                return RedirectToAction("Details", "Subjects", new { id = created.SubjectId });
            }
            catch (ResourceNotFoundException)
            {
                ModelState.AddModelError(nameof(model.SubjectId), "Môn học không tồn tại hoặc đã bị xóa.");
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        await LoadSubjectsAsync(cancellationToken);
        return View(model);
    }

    // GET: Topics/Edit/{id}
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var topic = await topics.GetAsync(id, cancellationToken);
        await LoadSubjectsAsync(cancellationToken);
        return View(TopicFormViewModel.From(topic));
    }

    // POST: Topics/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, TopicFormViewModel model, CancellationToken cancellationToken)
    {
        var current = await topics.GetAsync(id, cancellationToken);
        // The subject of a topic is fixed; ignore whatever the form posted.
        model.Id = id;
        model.SubjectId = current.SubjectId;
        ModelState.Remove(nameof(model.SubjectId));
        if (ModelState.IsValid)
        {
            try
            {
                await topics.UpdateAsync(id, new UpdateTopicRequest(model.Name, model.Description, model.Status),
                    cancellationToken);
                TempData.Toast("Đã lưu thay đổi.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        await LoadSubjectsAsync(cancellationToken);
        return View(model);
    }

    // GET: Topics/Delete/{id}
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await Details(id, cancellationToken);

    // POST: Topics/Delete/{id}
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var topic = await topics.GetAsync(id, cancellationToken);
        try
        {
            await topics.DeleteAsync(id, cancellationToken);
            TempData.Toast($"Đã xóa chủ đề \"{topic.Name}\".");
        }
        catch (ResourceConflictException)
        {
            TempData.Toast("Không thể xóa: chủ đề vẫn còn câu hỏi. Hãy xóa hoặc chuyển chúng trước.", success: false);
        }
        return RedirectToAction("Details", "Subjects", new { id = topic.SubjectId });
    }

    private async Task<IReadOnlyDictionary<Guid, SubjectResponse>> SubjectMapAsync(CancellationToken cancellationToken) =>
        (await subjects.ListAsync(new SubjectListQuery { PageSize = 100 }, cancellationToken)).Items.ToDictionary(x => x.Id);

    private async Task LoadSubjectsAsync(CancellationToken cancellationToken) =>
        ViewBag.Subjects = SelectLists.Subjects((await SubjectMapAsync(cancellationToken)).Values);

    private bool TryAddError(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validation:
                ModelState.AddErrors(validation);
                return true;
            case ResourceConflictException:
                ModelState.AddModelError(nameof(TopicFormViewModel.Name), "Môn học này đã có chủ đề cùng tên.");
                return true;
            case DomainValidationException domain:
                ModelState.AddModelError(string.Empty, domain.Message);
                return true;
            default:
                return false;
        }
    }
}
