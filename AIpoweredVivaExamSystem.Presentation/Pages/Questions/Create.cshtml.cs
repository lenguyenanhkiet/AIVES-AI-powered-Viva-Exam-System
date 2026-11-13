using AIpoweredVivaExamSystem.Business.Topics;
using AIpoweredVivaExamSystem.Business.Subjects;
using AIpoweredVivaExamSystem.Business.Questions;
using AIpoweredVivaExamSystem.DataAccess.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Questions;
using AIpoweredVivaExamSystem.DataAccess.Models.Subjects;
using AIpoweredVivaExamSystem.DataAccess.Models.Topics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Questions;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class CreateModel(QuestionService questions, SubjectService subjects, TopicService topics) : PageModel
{
    [BindProperty]
    public QuestionFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid? subjectId, Guid? topicId, CancellationToken cancellationToken)
    {
        Input = new QuestionFormViewModel { SubjectId = subjectId, TopicId = topicId };
        await LoadFormListsAsync(Input.SubjectId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await questions.CreateAsync(new CreateQuestionRequest(Input.SubjectId!.Value,
                    Input.TopicId, Input.Content, Input.ExpectedAnswer, Input.BloomLevel!.Value, Input.Difficulty!.Value),
                    cancellationToken);
                TempData.Toast("Đã tạo câu hỏi. Bước tiếp theo: thêm rubric chấm điểm.");
                return RedirectToPage("/Questions/Details", new { id = created.Id });
            }
            catch (ResourceNotFoundException)
            {
                ModelState.AddModelError(nameof(Input), "Môn học hoặc chủ đề đã chọn không còn tồn tại. Hãy chọn lại.");
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
