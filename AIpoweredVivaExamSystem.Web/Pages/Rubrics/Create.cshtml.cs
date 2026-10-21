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
public sealed class CreateModel(IRubricService rubrics, IQuestionService questions) : PageModel
{
    [BindProperty]
    public RubricFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid questionId, CancellationToken cancellationToken)
    {
        var question = await questions.GetAsync(questionId, cancellationToken);
        if (question.HasRubric)
            return RedirectToPage("/Questions/Details", new { id = questionId });
        Input = new RubricFormViewModel
        {
            Question = question,
            QuestionId = questionId,
            Criteria = [new CriterionFormViewModel()]
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Input.Id = null;
        foreach (var criterion in Input.Criteria)
            criterion.Id = null;
        if (Validate(Input))
        {
            try
            {
                await rubrics.CreateAsync(new CreateRubricRequest(Input.QuestionId, Input.Name, Input.Description,
                    Criteria(Input)), cancellationToken);
                TempData.Toast("Đã tạo rubric. Câu hỏi đã có thể gửi duyệt.");
                return RedirectToPage("/Questions/Details", new { id = Input.QuestionId });
            }
            catch (ResourceConflictException)
            {
                TempData.Toast("Câu hỏi này đã có rubric.", success: false);
                return RedirectToPage("/Questions/Details", new { id = Input.QuestionId });
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
