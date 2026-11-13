using AIpoweredVivaExamSystem.DataAccess.Models.Topics;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Business.Topics;

public sealed class TopicListQueryValidator : AbstractValidator<TopicListQuery>
{
    public TopicListQueryValidator()
    {
        RuleFor(x => x.SubjectId).Must(x => x != Guid.Empty);
        RuleFor(x => x.Keyword).MaximumLength(255);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
