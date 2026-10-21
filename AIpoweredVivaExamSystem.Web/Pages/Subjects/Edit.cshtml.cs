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


namespace AIpoweredVivaExamSystem.Web.Pages.Subjects;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN")]
public sealed class EditModel(ISubjectService subjects) : PageModel
{
    [BindProperty]
    public SubjectFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        Input = SubjectFormViewModel.From(await subjects.GetAsync(id, cancellationToken));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        Input.Id = id;
        if (!ModelState.IsValid)
            return Page();
        try
        {
            await subjects.UpdateAsync(id,
                new UpdateSubjectRequest(Input.Code, Input.Name, Input.Description, Input.Status), cancellationToken);
            TempData.Toast("Đã lưu thay đổi.");
            return RedirectToPage("/Subjects/Details", new { id });
        }
        catch (Exception exception) when (TryAddError(exception))
        {
            return Page();
        }
    }

    private bool TryAddError(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validation:
                ModelState.AddErrors(validation, nameof(Input));
                return true;
            case ResourceConflictException:
                ModelState.AddModelError($"Input.{nameof(SubjectFormViewModel.Code)}", "Mã môn này đã tồn tại.");
                return true;
            case DomainValidationException domain:
                ModelState.AddModelError(nameof(Input), domain.Message);
                return true;
            default:
                return false;
        }
    }
}
