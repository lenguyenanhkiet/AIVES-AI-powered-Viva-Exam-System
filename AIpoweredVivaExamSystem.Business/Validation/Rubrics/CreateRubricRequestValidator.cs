using AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Business.Rubrics;

public sealed class CreateRubricRequestValidator : AbstractValidator<CreateRubricRequest>
{
    public CreateRubricRequestValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Criteria).NotEmpty();
        RuleForEach(x => x.Criteria).NotNull().SetValidator(new CriterionRequestValidator());
        RuleForEach(x => x.Criteria).Must(x => x is null || x.Id is null)
            .WithMessage("New criteria must not include an Id.");
    }
}
