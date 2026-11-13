using AIpoweredVivaExamSystem.DataAccess.Models.Subjects;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Business.Subjects;

public sealed class SubjectListQueryValidator : AbstractValidator<SubjectListQuery>
{
    public SubjectListQueryValidator()
    {
        RuleFor(x => x.Keyword).MaximumLength(255);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
