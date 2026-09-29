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
        SubjectId = subjectId;
        Update(name, description, status);
    }

    // SubjectId is part of the alternate key used by Questions, so a topic cannot move between subjects.
    public void Update(string name, string? description, AcademicStatus status)
    {
        var normalizedName = DomainRules.RequiredText(name, nameof(Name), 255);
        DomainRules.DefinedEnum(status, nameof(Status));
        Name = normalizedName;
        Description = description;
        Status = status;
    }
}
