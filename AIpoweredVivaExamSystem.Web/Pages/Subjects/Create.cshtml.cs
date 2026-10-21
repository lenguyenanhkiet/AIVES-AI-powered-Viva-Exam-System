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
public sealed class CreateModel(ISubjectService subjects) : ContentPageModel
{
    public IActionResult OnGet() => RenderContent("/Pages/Subjects/_CreateContent.cshtml", new SubjectFormViewModel());

    public async Task<IActionResult> OnPostAsync(SubjectFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return RenderContent("/Pages/Subjects/_CreateContent.cshtml", model);
        try
        {
            var created = await subjects.CreateAsync(
                new CreateSubjectRequest(model.Code, model.Name, model.Description, model.Status), cancellationToken);
            TempData.Toast($"Đã tạo môn học {created.Code}.");
            return RedirectToPage("/Subjects/Details", new { id = created.Id });
        }
        catch (Exception exception) when (TryAddError(exception))
        {
            return RenderContent("/Pages/Subjects/_CreateContent.cshtml", model);
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
