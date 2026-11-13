using AIpoweredVivaExamSystem.DataAccess.Models.Subjects;
using FluentValidation;

namespace AIpoweredVivaExamSystem.Business.Subjects;

public sealed class CreateSubjectRequestValidator : AbstractValidator<CreateSubjectRequest>
{
    public CreateSubjectRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Status).IsInEnum();
    }
}
