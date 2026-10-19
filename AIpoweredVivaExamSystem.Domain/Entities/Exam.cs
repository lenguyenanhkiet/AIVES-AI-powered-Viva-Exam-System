using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public enum ExamStatus { Draft, Published, Ongoing, Completed, Cancelled }

// Shared scaffold. Exam CRUD/lifecycle orchestration belongs to Tue Minh's module.
public sealed class Exam : AuditableEntity, IAggregateRoot
{
    public Guid SubjectId { get; private set; }
    public Guid LecturerId { get; private set; }
    public string Name { get; private set; } = "";
    public DateTimeOffset StartAt { get; private set; }
    public DateTimeOffset EndAt { get; private set; }
    public int DurationMinutes { get; private set; }
    public int NumberOfMainQuestions { get; private set; }
    public int MaxFollowUpPerQuestion { get; private set; }
    public ExamStatus Status { get; private set; } = ExamStatus.Draft;
    public byte[] RowVersion { get; private set; } = [];
    private Exam() { }
    public Exam(Guid subjectId, Guid lecturerId, string name, DateTimeOffset startAt, DateTimeOffset endAt,
        int durationMinutes, int numberOfMainQuestions, int maxFollowUpPerQuestion)
    {
        DomainRules.RequiredId(subjectId, nameof(SubjectId));
        DomainRules.RequiredId(lecturerId, nameof(LecturerId));
        var validName = DomainRules.RequiredText(name, nameof(Name), 200);
        if (startAt >= endAt || durationMinutes <= 0 || endAt - startAt < TimeSpan.FromMinutes(durationMinutes)
            || numberOfMainQuestions <= 0 || maxFollowUpPerQuestion < 0)
            throw new DomainValidationException("Invalid exam window, duration or question configuration.");
        SubjectId = subjectId; LecturerId = lecturerId; Name = validName; StartAt = startAt; EndAt = endAt;
        DurationMinutes = durationMinutes; NumberOfMainQuestions = numberOfMainQuestions;
        MaxFollowUpPerQuestion = maxFollowUpPerQuestion;
    }
}
