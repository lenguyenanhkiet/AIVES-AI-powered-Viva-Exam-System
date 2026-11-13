using AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Business.Rubrics;

public sealed class UpdateRubricRequestValidator : AbstractValidator<UpdateRubricRequest>
{
    public UpdateRubricRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Criteria).NotEmpty();
        RuleForEach(x => x.Criteria).NotNull().SetValidator(new CriterionRequestValidator());
    }
}
