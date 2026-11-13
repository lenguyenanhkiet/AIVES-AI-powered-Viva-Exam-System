using AIpoweredVivaExamSystem.DataAccess.Models.Questions;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Business.Questions;

public sealed class QuestionListQueryValidator : AbstractValidator<QuestionListQuery>
{
    public QuestionListQueryValidator()
    {
        RuleFor(x => x.SubjectId).Must(x => x != Guid.Empty);
        RuleFor(x => x.TopicId).Must(x => x != Guid.Empty);
        RuleFor(x => x.Keyword).MaximumLength(255);
        RuleFor(x => x.BloomLevel).IsInEnum();
        RuleFor(x => x.Difficulty).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
