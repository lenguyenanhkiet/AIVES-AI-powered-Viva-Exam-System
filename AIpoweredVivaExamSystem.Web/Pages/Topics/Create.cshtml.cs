using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.Topics;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class CreateModel(ITopicService topics, ISubjectService subjects) : PageModel
{
    [BindProperty]
    public TopicFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid? subjectId, CancellationToken cancellationToken)
    {
        Input = new TopicFormViewModel { SubjectId = subjectId };
        await LoadSubjectsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await topics.CreateAsync(
                    new CreateTopicRequest(Input.SubjectId!.Value, Input.Name, Input.Description, Input.Status),
                    cancellationToken);
                TempData.Toast($"Đã tạo chủ đề \"{created.Name}\".");
                return RedirectToPage("/Subjects/Details", new { id = created.SubjectId });
            }
            catch (ResourceNotFoundException)
            {
                ModelState.AddModelError($"Input.{nameof(Input.SubjectId)}", "Môn học không tồn tại hoặc đã bị xóa.");
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        await LoadSubjectsAsync(cancellationToken);
        return Page();
    }

    private async Task LoadSubjectsAsync(CancellationToken cancellationToken) =>
        Input.Subjects = SelectLists.Subjects((await SubjectMapAsync(cancellationToken)).Values);

    private bool TryAddError(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validation:
                ModelState.AddErrors(validation, nameof(Input));
                return true;
            case ResourceConflictException:
                ModelState.AddModelError($"Input.{nameof(TopicFormViewModel.Name)}", "Môn học này đã có chủ đề cùng tên.");
                return true;
            case DomainValidationException domain:
                ModelState.AddModelError(nameof(Input), domain.Message);
                return true;
            default:
                return false;
        }
    }

    private async Task<IReadOnlyDictionary<Guid, SubjectResponse>> SubjectMapAsync(CancellationToken cancellationToken) =>
        (await subjects.ListAsync(new SubjectListQuery { PageSize = 100 }, cancellationToken)).Items.ToDictionary(x => x.Id);
}
