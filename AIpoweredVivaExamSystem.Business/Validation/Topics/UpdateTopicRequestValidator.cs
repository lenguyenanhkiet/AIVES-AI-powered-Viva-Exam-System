using AIpoweredVivaExamSystem.DataAccess.Models.Topics;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Business.Topics;

public sealed class UpdateTopicRequestValidator : AbstractValidator<UpdateTopicRequest>
{
    public UpdateTopicRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Status).IsInEnum();
    }
}
