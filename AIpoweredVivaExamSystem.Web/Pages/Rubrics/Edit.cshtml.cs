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
public sealed class EditModel(IRubricService rubrics, IQuestionService questions) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var rubric = await rubrics.GetAsync(id, cancellationToken);
        ViewData["Question"] = await questions.GetAsync(rubric.QuestionId, cancellationToken);
        return RenderContent("/Pages/Rubrics/_EditContent.cshtml", RubricFormViewModel.From(rubric));
    }

    public async Task<IActionResult> OnPostAsync(Guid id, RubricFormViewModel model, CancellationToken cancellationToken)
    {
        var current = await rubrics.GetAsync(id, cancellationToken);
        model.Id = id;
        model.QuestionId = current.QuestionId;
        if (Validate(model))
        {
            try
            {
                await rubrics.UpdateAsync(id, new UpdateRubricRequest(model.Name, model.Description, Criteria(model)),
                    cancellationToken);
                TempData.Toast("Đã lưu rubric.");
                return RedirectToPage("/Questions/Details", new { id = model.QuestionId });
            }
            catch (ResourceConflictException)
            {
                ModelState.AddModelError(string.Empty, "Rubric vừa được người khác sửa. Tải lại trang rồi thử lại.");
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        ViewData["Question"] = await questions.GetAsync(model.QuestionId, cancellationToken);
        return RenderContent("/Pages/Rubrics/_EditContent.cshtml", model);
    }

    private bool Validate(RubricFormViewModel model)
    {
        if (model.Criteria.Count == 0)
            ModelState.AddModelError(string.Empty, "Rubric cần ít nhất một tiêu chí.");
        else if (model.Criteria.Sum(x => x.MaxScore ?? 0) > 999.99m)
            ModelState.AddModelError(string.Empty, "Tổng điểm không được vượt quá 999.99.");
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
                ModelState.AddErrors(validation);
                return true;
            case DomainValidationException domain:
                ModelState.AddModelError(string.Empty, domain.Message);
                return true;
            default:
                return false;
        }
    }
}
