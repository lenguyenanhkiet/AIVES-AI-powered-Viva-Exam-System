using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.Exams;

// Handoff contracts; each member implements and registers their module. These are not placeholder services.
public sealed record ExamConfiguration(Guid SubjectId, string Name, DateTimeOffset StartAt, DateTimeOffset EndAt,
    int DurationMinutes, int NumberOfMainQuestions, int MaxFollowUpPerQuestion);
public sealed record ExamDetails(ExamSummary Summary, int DurationMinutes, int MainQuestions, int MaxFollowUps, byte[] RowVersion);
public interface IExamService
{
    Task<ExamDetails> CreateAsync(ExamConfiguration request, CancellationToken ct);
    Task<ExamDetails> GetAsync(Guid examId, CancellationToken ct);
    Task UpdateAsync(Guid examId, ExamConfiguration request, byte[] expectedVersion, CancellationToken ct);
    Task TransitionAsync(Guid examId, ExamStatus target, byte[] expectedVersion, CancellationToken ct);
}
public sealed record PoolQuestion(Guid QuestionId, string Content);
public interface IExamPoolService
{
    Task<IReadOnlyList<PoolQuestion>> ListEligibleAsync(Guid examId, CancellationToken ct);
    Task<int> CountEligibleAsync(Guid examId, CancellationToken ct);
    Task AddAsync(Guid examId, IReadOnlyCollection<Guid> questionIds, CancellationToken ct);
    Task RemoveAsync(Guid examId, Guid questionId, CancellationToken ct);
}
public sealed record RosterEntry(Guid EnrollmentId, Guid StudentId, string Name, DateTimeOffset? ScheduledAt, byte[] RowVersion);
public interface IExamRosterService
{
    Task<IReadOnlyList<RosterEntry>> ListAsync(Guid examId, CancellationToken ct);
    Task AddAsync(Guid examId, string studentEmail, CancellationToken ct);
    Task RemoveAsync(Guid examId, Guid enrollmentId, CancellationToken ct);
}
// PreviewToken must bind exam/roster versions and intended slots; revalidate on Apply.
public sealed record ScheduleSlot(Guid EnrollmentId, DateTimeOffset StartAt, DateTimeOffset EndAt);
public sealed record SchedulePreview(string PreviewToken, IReadOnlyList<ScheduleSlot> Slots);
public interface IExamScheduleService
{
    Task<SchedulePreview> PreviewAsync(Guid examId, CancellationToken ct);
    Task ApplyAsync(Guid examId, string previewToken, CancellationToken ct);
    Task<IReadOnlyList<ScheduleSlot>> MyScheduleAsync(CancellationToken ct);
}
public sealed record StudentSession(Guid Id, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, IReadOnlyList<StudentQuestion> Questions);
public interface IVivaSessionService
{
    Task<StudentSession> GetOrCreateMySessionAsync(Guid enrollmentId, CancellationToken ct);
    Task CompleteMySessionAsync(Guid sessionId, CancellationToken ct);
}
