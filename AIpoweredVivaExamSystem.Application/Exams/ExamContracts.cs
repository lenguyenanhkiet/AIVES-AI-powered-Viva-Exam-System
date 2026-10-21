using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.Exams;

public sealed record ExamSummary(Guid Id, Guid SubjectId, string Name, ExamStatus Status,
    DateTimeOffset StartAt, DateTimeOffset EndAt);
public sealed record ExamAccess(Guid Id, Guid SubjectId, Guid LecturerId);
public sealed record ExamStudentAccess(Guid Id, Guid ExamId, Guid StudentId);
public sealed record ExamFoundationCounts(int PoolQuestions, int Students, int ScheduledStudents, int Sessions);
// This is the only session-question shape intended for student clients.
public sealed record StudentQuestion(Guid Id, int Position, string Content);

public interface IExamAccessStore
{
    Task<ExamAccess?> FindExamAsync(Guid examId, CancellationToken ct);
    // Retained access facts, including soft-deleted exams, for list-change notifications only.
    Task<ExamAccess?> FindExamForNotificationAsync(Guid examId, CancellationToken ct);
    Task<ExamStudentAccess?> FindEnrollmentAsync(Guid enrollmentId, CancellationToken ct);
    Task<bool> IsAssignedAsync(Guid lecturerId, Guid subjectId, CancellationToken ct);
    Task<bool> IsEnrolledAsync(Guid examId, Guid studentId, CancellationToken ct);
}
public interface IExamFoundationReader
{
    Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct);
    Task<ExamFoundationCounts> CountsAsync(Guid examId, CancellationToken ct);
}
public interface IExamNotifier
{
    Task NotifyChangedAsync(Guid examId, CancellationToken cancellationToken);
}
public interface IExamUnitOfWork
{
    // Work may stage entities only. No external calls or independent commits inside the delegate.
    // changedExamIds are delivered only after a successful database commit.
    Task ExecuteAsync(Func<CancellationToken, Task> work, IReadOnlyCollection<Guid> changedExamIds, CancellationToken ct);
}
