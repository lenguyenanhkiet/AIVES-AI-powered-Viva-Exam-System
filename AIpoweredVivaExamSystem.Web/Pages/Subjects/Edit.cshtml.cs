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


namespace AIpoweredVivaExamSystem.Web.Pages.Subjects;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN")]
public sealed class EditModel(ISubjectService subjects) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken) =>
        RenderContent("/Pages/Subjects/_EditContent.cshtml", SubjectFormViewModel.From(await subjects.GetAsync(id, cancellationToken)));

    public async Task<IActionResult> OnPostAsync(Guid id, SubjectFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!ModelState.IsValid)
            return RenderContent("/Pages/Subjects/_EditContent.cshtml", model);
        try
        {
            await subjects.UpdateAsync(id,
                new UpdateSubjectRequest(model.Code, model.Name, model.Description, model.Status), cancellationToken);
            TempData.Toast("Đã lưu thay đổi.");
            return RedirectToPage("/Subjects/Details", new { id });
        }
        catch (Exception exception) when (TryAddError(exception))
        {
            return RenderContent("/Pages/Subjects/_EditContent.cshtml", model);
        }
    }

    private bool TryAddError(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validation:
                ModelState.AddErrors(validation);
                return true;
            case ResourceConflictException:
                ModelState.AddModelError(nameof(SubjectFormViewModel.Code), "Mã môn này đã tồn tại.");
                return true;
            case DomainValidationException domain:
                ModelState.AddModelError(string.Empty, domain.Message);
                return true;
            default:
                return false;
        }
    }
}
