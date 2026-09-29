using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Application.Topics;

public sealed class CreateTopicRequestValidator : AbstractValidator<CreateTopicRequest>
{
    public CreateTopicRequestValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class UpdateTopicRequestValidator : AbstractValidator<UpdateTopicRequest>
{
    public UpdateTopicRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Status).IsInEnum();
    }
}

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
