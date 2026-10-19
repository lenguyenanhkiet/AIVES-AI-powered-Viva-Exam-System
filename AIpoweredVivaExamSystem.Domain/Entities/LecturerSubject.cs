using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public sealed class LecturerSubject : BaseEntity
{
    public Guid LecturerId { get; private set; }
    public Guid SubjectId { get; private set; }
    private LecturerSubject() { }
    public LecturerSubject(Guid lecturerId, Guid subjectId)
    {
        DomainRules.RequiredId(lecturerId, nameof(LecturerId));
        DomainRules.RequiredId(subjectId, nameof(SubjectId));
        LecturerId = lecturerId; SubjectId = subjectId;
    }
}
