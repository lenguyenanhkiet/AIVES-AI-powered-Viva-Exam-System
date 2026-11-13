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
public sealed class EditModel(TopicService topics, SubjectService subjects) : PageModel
{
    [BindProperty]
    public TopicFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var topic = await topics.GetAsync(id, cancellationToken);
        Input = TopicFormViewModel.From(topic);
        await LoadSubjectsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        var current = await topics.GetAsync(id, cancellationToken);
        // The subject of a topic is fixed; ignore whatever the form posted.
        Input.Id = id;
        Input.SubjectId = current.SubjectId;
        ModelState.Remove($"Input.{nameof(Input.SubjectId)}");
        if (ModelState.IsValid)
        {
            try
            {
                await topics.UpdateAsync(id, new UpdateTopicRequest(Input.Name, Input.Description, Input.Status),
                    cancellationToken);
                TempData.Toast("Đã lưu thay đổi.");
                return RedirectToPage("/Topics/Details", new { id });
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
