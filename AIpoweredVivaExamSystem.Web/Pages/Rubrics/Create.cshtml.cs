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
public sealed class CreateModel(IRubricService rubrics, IQuestionService questions) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(Guid questionId, CancellationToken cancellationToken)
    {
        var question = await questions.GetAsync(questionId, cancellationToken);
        if (question.HasRubric)
            return RedirectToPage("/Questions/Details", new { id = questionId });
        ViewData["Question"] = question;
        return RenderContent("/Pages/Rubrics/_CreateContent.cshtml", new RubricFormViewModel
        {
            QuestionId = questionId,
            Criteria = [new CriterionFormViewModel()]
        });
    }

    public async Task<IActionResult> OnPostAsync(RubricFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = null;
        foreach (var criterion in model.Criteria)
            criterion.Id = null;
        if (Validate(model))
        {
            try
            {
                await rubrics.CreateAsync(new CreateRubricRequest(model.QuestionId, model.Name, model.Description,
                    Criteria(model)), cancellationToken);
                TempData.Toast("Đã tạo rubric. Câu hỏi đã có thể gửi duyệt.");
                return RedirectToPage("/Questions/Details", new { id = model.QuestionId });
            }
            catch (ResourceConflictException)
            {
                TempData.Toast("Câu hỏi này đã có rubric.", success: false);
                return RedirectToPage("/Questions/Details", new { id = model.QuestionId });
            }
            catch (Exception exception) when (TryAddError(exception)) { }
        }
        ViewData["Question"] = await questions.GetAsync(model.QuestionId, cancellationToken);
        return RenderContent("/Pages/Rubrics/_CreateContent.cshtml", model);
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
