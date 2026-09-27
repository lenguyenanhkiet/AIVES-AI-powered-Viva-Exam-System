using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public class Subject : AuditableEntity, IAggregateRoot
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public AcademicStatus Status { get; private set; }

    private Subject() { }

    public Subject(string code, string name, string? description = null,
        AcademicStatus status = AcademicStatus.Active)
    {
        Code = DomainRules.RequiredText(code, nameof(Code), 50);
        Name = DomainRules.RequiredText(name, nameof(Name), 255);
        DomainRules.DefinedEnum(status, nameof(Status));
        Description = description;
        Status = status;
    }
}
