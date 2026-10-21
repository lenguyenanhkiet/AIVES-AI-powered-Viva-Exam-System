using Microsoft.AspNetCore.Mvc.RazorPages;
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
public sealed class EditModel(IQuestionService questions, ISubjectService subjects, ITopicService topics) : PageModel
{
    [BindProperty]
    public QuestionFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var question = await questions.GetAsync(id, cancellationToken);
        Input = QuestionFormViewModel.From(question);
        await LoadFormListsAsync(question.SubjectId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        var current = await questions.GetAsync(id, cancellationToken);
        // The subject of a question is fixed; ignore whatever the form posted.
        Input.Id = id;
        Input.SubjectId = current.SubjectId;
        Input.CurrentStatus = current.Status;
        ModelState.Remove($"Input.{nameof(Input.SubjectId)}");
        if (ModelState.IsValid)
        {
            try
            {
                await questions.UpdateAsync(id, new UpdateQuestionRequest(Input.TopicId, Input.Content,
                    Input.ExpectedAnswer, Input.BloomLevel!.Value, Input.Difficulty!.Value), cancellationToken);
                TempData.Toast(current.Status == QuestionStatus.Draft
                    ? "Đã lưu thay đổi."
                    : "Đã lưu thay đổi. Câu hỏi được chuyển về Nháp để duyệt lại.");
                return RedirectToPage("/Questions/Details", new { id });
            }
            catch (ResourceNotFoundException)
            {
                ModelState.AddModelError($"Input.{nameof(Input.TopicId)}", "Chủ đề đã chọn không còn tồn tại.");
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        await LoadFormListsAsync(Input.SubjectId, cancellationToken);
        return Page();
    }

    private async Task LoadFormListsAsync(Guid? subjectId, CancellationToken cancellationToken)
    {
        Input.Subjects = SelectLists.Subjects((await SubjectMapAsync(cancellationToken)).Values);
        Input.Topics = subjectId is null
            ? Array.Empty<SelectListItem>()
            : (await TopicMapAsync(subjectId, cancellationToken)).Values
                .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToArray();
    }

    private bool TryAddError(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validation:
                ModelState.AddErrors(validation, nameof(Input));
                return true;
            case ResourceConflictException:
                ModelState.AddModelError(nameof(Input), "Môn học hoặc chủ đề vừa bị xóa. Tải lại trang và thử lại.");
                return true;
            case DomainValidationException:
                ModelState.AddModelError($"Input.{nameof(QuestionFormViewModel.TopicId)}", "Chủ đề phải thuộc môn học đã chọn.");
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
