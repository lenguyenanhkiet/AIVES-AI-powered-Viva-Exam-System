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
public sealed class CreateModel(ITopicService topics, ISubjectService subjects) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid? subjectId, CancellationToken cancellationToken)
    {
        await LoadSubjectsAsync(cancellationToken);
        return RenderContent("/Pages/Topics/_CreateContent.cshtml", new TopicFormViewModel { SubjectId = subjectId });
    }

    public async Task<IActionResult> OnPostAsync(TopicFormViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await topics.CreateAsync(
                    new CreateTopicRequest(model.SubjectId!.Value, model.Name, model.Description, model.Status),
                    cancellationToken);
                TempData.Toast($"Đã tạo chủ đề \"{created.Name}\".");
                return RedirectToPage("/Subjects/Details", new { id = created.SubjectId });
            }
            catch (ResourceNotFoundException)
            {
                ModelState.AddModelError(nameof(model.SubjectId), "Môn học không tồn tại hoặc đã bị xóa.");
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        await LoadSubjectsAsync(cancellationToken);
        return RenderContent("/Pages/Topics/_CreateContent.cshtml", model);
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
