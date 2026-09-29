using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.Models;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AIpoweredVivaExamSystem.Web.Controllers;

public sealed class QuestionsController(
    IQuestionService questions, ISubjectService subjects, ITopicService topics, IRubricService rubrics) : Controller
{
    private static readonly QuestionStatus[] StatusTabs =
        [QuestionStatus.Draft, QuestionStatus.Approved, QuestionStatus.Rejected];

    // GET: Questions?subjectId=&topicId=&keyword=&bloomLevel=&difficulty=&status=&page=
    public async Task<IActionResult> Index(QuestionListQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || query.SubjectId == Guid.Empty || query.TopicId == Guid.Empty
            || query.Page < 1 || query.PageSize is < 1 or > 100
            || (query.BloomLevel is { } bloom && !Enum.IsDefined(bloom))
            || (query.Difficulty is { } difficulty && !Enum.IsDefined(difficulty))
            || (query.Status is { } status && !Enum.IsDefined(status)))
            query = new QuestionListQuery();

        var page = await questions.ListAsync(query, cancellationToken);
        var counts = new Dictionary<QuestionStatus, int>();
        foreach (var tab in StatusTabs)
            counts[tab] = (await questions.ListAsync(CountQuery(query, tab), cancellationToken)).TotalCount;
        var all = (await questions.ListAsync(CountQuery(query, null), cancellationToken)).TotalCount;

        return View(new QuestionIndexViewModel
        {
            Page = page, Query = query, StatusCounts = counts, AllCount = all,
            Subjects = await SubjectMapAsync(cancellationToken),
            Topics = await TopicMapAsync(query.SubjectId, cancellationToken)
        });
    }

    // GET: Questions/Details/{id}
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var question = await questions.GetAsync(id, cancellationToken);
        var rubric = question.HasRubric
            ? (await rubrics.ListAsync(new RubricListQuery { QuestionId = id, PageSize = 1 }, cancellationToken)).Items.FirstOrDefault()
            : null;
        return View(new QuestionDetailsViewModel
        {
            Question = question,
            Subject = await subjects.GetAsync(question.SubjectId, cancellationToken),
            Topic = question.TopicId is { } topicId ? await topics.GetAsync(topicId, cancellationToken) : null,
            Rubric = rubric
        });
    }

    // GET: Questions/Create?subjectId=&topicId=
    public async Task<IActionResult> Create(Guid? subjectId, Guid? topicId, CancellationToken cancellationToken)
    {
        var model = new QuestionFormViewModel { SubjectId = subjectId, TopicId = topicId };
        await LoadFormListsAsync(model.SubjectId, cancellationToken);
        return View(model);
    }

    // POST: Questions/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuestionFormViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await questions.CreateAsync(new CreateQuestionRequest(model.SubjectId!.Value,
                    model.TopicId, model.Content, model.ExpectedAnswer, model.BloomLevel!.Value, model.Difficulty!.Value),
                    cancellationToken);
                TempData.Toast("Đã tạo câu hỏi. Bước tiếp theo: thêm rubric chấm điểm.");
                return RedirectToAction(nameof(Details), new { id = created.Id });
            }
            catch (ResourceNotFoundException)
            {
                ModelState.AddModelError(string.Empty, "Môn học hoặc chủ đề đã chọn không còn tồn tại. Hãy chọn lại.");
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        await LoadFormListsAsync(model.SubjectId, cancellationToken);
        return View(model);
    }

    // GET: Questions/Edit/{id}
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var question = await questions.GetAsync(id, cancellationToken);
        await LoadFormListsAsync(question.SubjectId, cancellationToken);
        return View(QuestionFormViewModel.From(question));
    }

    // POST: Questions/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, QuestionFormViewModel model, CancellationToken cancellationToken)
    {
        var current = await questions.GetAsync(id, cancellationToken);
        // The subject of a question is fixed; ignore whatever the form posted.
        model.Id = id;
        model.SubjectId = current.SubjectId;
        model.CurrentStatus = current.Status;
        ModelState.Remove(nameof(model.SubjectId));
        if (ModelState.IsValid)
        {
            try
            {
                await questions.UpdateAsync(id, new UpdateQuestionRequest(model.TopicId, model.Content,
                    model.ExpectedAnswer, model.BloomLevel!.Value, model.Difficulty!.Value), cancellationToken);
                TempData.Toast(current.Status == QuestionStatus.Draft
                    ? "Đã lưu thay đổi."
                    : "Đã lưu thay đổi. Câu hỏi được chuyển về Nháp để duyệt lại.");
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (ResourceNotFoundException)
            {
                ModelState.AddModelError(nameof(model.TopicId), "Chủ đề đã chọn không còn tồn tại.");
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        await LoadFormListsAsync(model.SubjectId, cancellationToken);
        return View(model);
    }

    // GET: Questions/Delete/{id}
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        View(await questions.GetAsync(id, cancellationToken));

    // POST: Questions/Delete/{id}
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        await questions.DeleteAsync(id, cancellationToken);
        TempData.Toast("Đã xóa câu hỏi.");
        return RedirectToAction(nameof(Index));
    }

    // POST: Questions/Approve/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await questions.ApproveAsync(id, cancellationToken);
            TempData.Toast("Đã duyệt câu hỏi. Câu hỏi sẵn sàng dùng cho kỳ thi.");
        }
        catch (ResourceConflictException)
        {
            TempData.Toast("Chưa thể duyệt: câu hỏi cần có rubric chấm điểm trước.", success: false);
        }
        catch (DomainValidationException)
        {
            TempData.Toast("Chỉ duyệt được câu hỏi đang ở trạng thái Nháp.", success: false);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: Questions/Reject/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await questions.RejectAsync(id, cancellationToken);
            TempData.Toast("Đã từ chối câu hỏi. Sửa lại nội dung để gửi duyệt lần nữa.");
        }
        catch (DomainValidationException)
        {
            TempData.Toast("Chỉ từ chối được câu hỏi đang ở trạng thái Nháp.", success: false);
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: Questions/TopicOptions?subjectId= — feeds the topic dropdown when the subject changes.
    public async Task<IActionResult> TopicOptions(Guid subjectId, CancellationToken cancellationToken)
    {
        if (subjectId == Guid.Empty)
            return Json(Array.Empty<object>());
        var items = await topics.ListAsync(new TopicListQuery { SubjectId = subjectId, PageSize = 100 }, cancellationToken);
        return Json(items.Items.Select(x => new { id = x.Id, name = x.Name }));
    }

    private static QuestionListQuery CountQuery(QuestionListQuery query, QuestionStatus? status) => new()
    {
        SubjectId = query.SubjectId, TopicId = query.TopicId, Keyword = query.Keyword,
        BloomLevel = query.BloomLevel, Difficulty = query.Difficulty, Status = status, PageSize = 1
    };

    private async Task<IReadOnlyDictionary<Guid, SubjectResponse>> SubjectMapAsync(CancellationToken cancellationToken) =>
        (await subjects.ListAsync(new SubjectListQuery { PageSize = 100 }, cancellationToken)).Items.ToDictionary(x => x.Id);

    private async Task<IReadOnlyDictionary<Guid, TopicResponse>> TopicMapAsync(Guid? subjectId, CancellationToken cancellationToken) =>
        (await topics.ListAsync(new TopicListQuery { SubjectId = subjectId, PageSize = 100 }, cancellationToken)).Items.ToDictionary(x => x.Id);

    private async Task LoadFormListsAsync(Guid? subjectId, CancellationToken cancellationToken)
    {
        ViewBag.Subjects = SelectLists.Subjects((await SubjectMapAsync(cancellationToken)).Values);
        ViewBag.Topics = subjectId is null
            ? Array.Empty<SelectListItem>()
            : (await TopicMapAsync(subjectId, cancellationToken)).Values
                .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToArray();
    }

    private bool TryAddError(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validation:
                ModelState.AddErrors(validation);
                return true;
            case ResourceConflictException:
                ModelState.AddModelError(string.Empty, "Môn học hoặc chủ đề vừa bị xóa. Tải lại trang và thử lại.");
                return true;
            case DomainValidationException:
                ModelState.AddModelError(nameof(QuestionFormViewModel.TopicId), "Chủ đề phải thuộc môn học đã chọn.");
                return true;
            default:
                return false;
        }
    }
}
