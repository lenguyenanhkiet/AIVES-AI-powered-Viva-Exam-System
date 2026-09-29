using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public class Topic : AuditableEntity
{
    public Guid SubjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public AcademicStatus Status { get; private set; }

    private Topic() { }

    public Topic(Guid subjectId, string name, string? description = null,
        AcademicStatus status = AcademicStatus.Active)
    {
        DomainRules.RequiredId(subjectId, nameof(SubjectId));
        DomainRules.DefinedEnum(status, nameof(Status));
        SubjectId = subjectId;
        Name = DomainRules.RequiredText(name, nameof(Name), 255);
        Description = description;
        Status = status;
    }
}
