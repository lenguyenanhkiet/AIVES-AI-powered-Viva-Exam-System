using Microsoft.AspNetCore.Mvc.RazorPages;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Web.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;


namespace AIpoweredVivaExamSystem.Web.Pages.Rubrics;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = "ADMIN,LECTURER")]
public sealed class EditModel(IRubricService rubrics, IQuestionService questions) : PageModel
{
    [BindProperty]
    public RubricFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var rubric = await rubrics.GetAsync(id, cancellationToken);
        Input = RubricFormViewModel.From(rubric);
        Input.Question = await questions.GetAsync(rubric.QuestionId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        var current = await rubrics.GetAsync(id, cancellationToken);
        Input.Id = id;
        Input.QuestionId = current.QuestionId;
        if (Validate(Input))
        {
            try
            {
                await rubrics.UpdateAsync(id, new UpdateRubricRequest(Input.Name, Input.Description, Criteria(Input)),
                    cancellationToken);
                TempData.Toast("Đã lưu rubric.");
                return RedirectToPage("/Questions/Details", new { id = Input.QuestionId });
            }
            catch (ResourceConflictException)
            {
                ModelState.AddModelError(nameof(Input), "Rubric vừa được người khác sửa. Tải lại trang rồi thử lại.");
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        Input.Question = await questions.GetAsync(Input.QuestionId, cancellationToken);
        return Page();
    }

    private bool Validate(RubricFormViewModel model)
    {
        if (model.Criteria.Count == 0)
            ModelState.AddModelError(nameof(Input), "Rubric cần ít nhất một tiêu chí.");
        else if (model.Criteria.Sum(x => x.MaxScore ?? 0) > 999.99m)
            ModelState.AddModelError(nameof(Input), "Tổng điểm không được vượt quá 999.99.");
        return ModelState.IsValid;
    }

    private static List<CriterionRequest> Criteria(RubricFormViewModel model) =>
        model.Criteria.Select((x, i) => new CriterionRequest(x.Name, x.Description, x.ExpectedConcepts,
            x.MaxScore!.Value, i + 1, x.Id)).ToList();

    private bool TryAddError(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validation:
                ModelState.AddErrors(validation, nameof(Input));
                return true;
            case DomainValidationException domain:
                ModelState.AddModelError(nameof(Input), domain.Message);
                return true;
            default:
                return false;
        }
    }
}
