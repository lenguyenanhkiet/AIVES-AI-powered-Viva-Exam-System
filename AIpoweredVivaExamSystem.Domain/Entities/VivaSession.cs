using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public sealed class VivaSession : BaseEntity
{
    public Guid ExamStudentId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private VivaSession() { }
    public VivaSession(Guid examStudentId, DateTimeOffset startedAt)
    {
        DomainRules.RequiredId(examStudentId, nameof(ExamStudentId));
        ExamStudentId = examStudentId; StartedAt = startedAt;
    }
}
