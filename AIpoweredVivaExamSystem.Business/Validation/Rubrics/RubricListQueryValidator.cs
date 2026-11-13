using AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Business.Rubrics;

public sealed class RubricListQueryValidator : AbstractValidator<RubricListQuery>
{
    public RubricListQueryValidator()
    {
        RuleFor(x => x.QuestionId).Must(x => x != Guid.Empty);
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
