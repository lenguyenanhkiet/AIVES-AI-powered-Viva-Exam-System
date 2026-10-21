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
public sealed class EditModel(ITopicService topics, ISubjectService subjects) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var topic = await topics.GetAsync(id, cancellationToken);
        await LoadSubjectsAsync(cancellationToken);
        return RenderContent("/Pages/Topics/_EditContent.cshtml", TopicFormViewModel.From(topic));
    }

    public async Task<IActionResult> OnPostAsync(Guid id, TopicFormViewModel model, CancellationToken cancellationToken)
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
                return RedirectToPage("/Topics/Details", new { id });
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        await LoadSubjectsAsync(cancellationToken);
        return RenderContent("/Pages/Topics/_EditContent.cshtml", model);
    }

    private async Task LoadSubjectsAsync(CancellationToken cancellationToken) =>
        ViewData["Subjects"] = SelectLists.Subjects((await SubjectMapAsync(cancellationToken)).Values);

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

    private async Task<IReadOnlyDictionary<Guid, SubjectResponse>> SubjectMapAsync(CancellationToken cancellationToken) =>
        (await subjects.ListAsync(new SubjectListQuery { PageSize = 100 }, cancellationToken)).Items.ToDictionary(x => x.Id);
}
