using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Application.Questions;

public sealed class CreateQuestionRequestValidator : AbstractValidator<CreateQuestionRequest>
{
    public CreateQuestionRequestValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.TopicId).Must(x => x != Guid.Empty);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ExpectedAnswer).MaximumLength(8000);
        RuleFor(x => x.BloomLevel).IsInEnum();
        RuleFor(x => x.Difficulty).IsInEnum();
    }
}

public sealed class UpdateQuestionRequestValidator : AbstractValidator<UpdateQuestionRequest>
{
    public UpdateQuestionRequestValidator()
    {
        RuleFor(x => x.TopicId).Must(x => x != Guid.Empty);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ExpectedAnswer).MaximumLength(8000);
        RuleFor(x => x.BloomLevel).IsInEnum();
        RuleFor(x => x.Difficulty).IsInEnum();
    }
}

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
