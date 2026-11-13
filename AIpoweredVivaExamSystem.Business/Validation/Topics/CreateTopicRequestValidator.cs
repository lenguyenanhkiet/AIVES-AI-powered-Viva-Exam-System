using AIpoweredVivaExamSystem.DataAccess.Models.Topics;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Business.Topics;

public sealed class CreateTopicRequestValidator : AbstractValidator<CreateTopicRequest>
{
    public CreateTopicRequestValidator()
    {
        RuleFor(x => x.SubjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Status).IsInEnum();
    }
}
