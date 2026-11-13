using AIpoweredVivaExamSystem.Business.Topics;
using AIpoweredVivaExamSystem.Business.Subjects;
using AIpoweredVivaExamSystem.DataAccess.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Models.Subjects;
using AIpoweredVivaExamSystem.DataAccess.Models.Topics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Presentation.Pages.Topics;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class CreateModel(TopicService topics, SubjectService subjects) : PageModel
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
