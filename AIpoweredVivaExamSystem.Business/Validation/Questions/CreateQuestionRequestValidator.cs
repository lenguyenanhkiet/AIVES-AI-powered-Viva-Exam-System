using AIpoweredVivaExamSystem.DataAccess.Models.Questions;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Business.Questions;

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
