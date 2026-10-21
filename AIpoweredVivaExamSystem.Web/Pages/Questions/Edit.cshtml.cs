using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Web.Pages.Questions;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class EditModel(IQuestionService questions, ISubjectService subjects, ITopicService topics) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var question = await questions.GetAsync(id, cancellationToken);
        await LoadFormListsAsync(question.SubjectId, cancellationToken);
        return RenderContent("/Pages/Questions/_EditContent.cshtml", QuestionFormViewModel.From(question));
    }

    public async Task<IActionResult> OnPostAsync(Guid id, QuestionFormViewModel model, CancellationToken cancellationToken)
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
                return RedirectToPage("/Questions/Details", new { id });
            }
            catch (ResourceNotFoundException)
            {
                ModelState.AddModelError(nameof(model.TopicId), "Chủ đề đã chọn không còn tồn tại.");
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        await LoadFormListsAsync(model.SubjectId, cancellationToken);
        return RenderContent("/Pages/Questions/_EditContent.cshtml", model);
    }

    private async Task LoadFormListsAsync(Guid? subjectId, CancellationToken cancellationToken)
    {
        ViewData["Subjects"] = SelectLists.Subjects((await SubjectMapAsync(cancellationToken)).Values);
        ViewData["Topics"] = subjectId is null
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

    private async Task<IReadOnlyDictionary<Guid, SubjectResponse>> SubjectMapAsync(CancellationToken cancellationToken) =>
        (await subjects.ListAsync(new SubjectListQuery { PageSize = 100 }, cancellationToken)).Items.ToDictionary(x => x.Id);

    private async Task<IReadOnlyDictionary<Guid, TopicResponse>> TopicMapAsync(Guid? subjectId, CancellationToken cancellationToken) =>
        (await topics.ListAsync(new TopicListQuery { SubjectId = subjectId, PageSize = 100 }, cancellationToken)).Items.ToDictionary(x => x.Id);
}
