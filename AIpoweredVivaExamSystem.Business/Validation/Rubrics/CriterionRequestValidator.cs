using AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Business.Rubrics;

public sealed class CriterionRequestValidator : AbstractValidator<CriterionRequest>
{
    public CriterionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.MaxScore).GreaterThan(0).LessThanOrEqualTo(999.99m)
            .Must(x => decimal.Round(x, 2) == x).WithMessage("MaxScore must have at most two decimal places.");
        RuleFor(x => x.DisplayOrder).GreaterThan(0);
        RuleFor(x => x.Id).Must(x => x != Guid.Empty).WithMessage("Id must not be an empty GUID.");
    }
}
