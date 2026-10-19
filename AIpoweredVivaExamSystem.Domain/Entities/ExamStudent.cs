using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public sealed class ExamStudent : BaseEntity
{
    public Guid ExamId { get; private set; }
    public Guid StudentId { get; private set; }
    public DateTimeOffset? ScheduledAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private ExamStudent() { }
    public ExamStudent(Guid examId, Guid studentId)
    {
        DomainRules.RequiredId(examId, nameof(ExamId)); DomainRules.RequiredId(studentId, nameof(StudentId));
        ExamId = examId; StudentId = studentId;
    }
    // Scheduling service must validate window, overlap, lifecycle and ownership before calling this.
    public void Schedule(DateTimeOffset scheduledAt) => ScheduledAt = scheduledAt;
}
