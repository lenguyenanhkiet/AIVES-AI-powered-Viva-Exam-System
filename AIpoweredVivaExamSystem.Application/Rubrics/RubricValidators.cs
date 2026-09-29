using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Application.Rubrics;

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

public sealed class UpdateRubricRequestValidator : AbstractValidator<UpdateRubricRequest>
{
    public UpdateRubricRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Criteria).NotEmpty();
        RuleForEach(x => x.Criteria).NotNull().SetValidator(new CriterionRequestValidator());
    }
}

public sealed class RubricListQueryValidator : AbstractValidator<RubricListQuery>
{
    public RubricListQueryValidator()
    {
        RuleFor(x => x.QuestionId).Must(x => x != Guid.Empty);
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
