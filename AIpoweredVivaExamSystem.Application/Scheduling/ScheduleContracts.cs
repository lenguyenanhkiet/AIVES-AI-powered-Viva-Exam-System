using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.Scheduling;

public sealed record ScheduleEnrollment(Guid EnrollmentId, Guid StudentId, string StudentName, string Email,
    DateTimeOffset? ScheduledAt, byte[] RowVersion);
public sealed record ExamSchedule(ExamSummary Exam, int DurationMinutes, IReadOnlyList<ScheduleEnrollment> Roster);
public sealed record PersonalSchedule(Guid EnrollmentId, string ExamName, ExamStatus Status,
    DateTimeOffset StartAt, DateTimeOffset EndAt);

public interface IScheduleReader
{
    Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct);
    Task<ExamSchedule> GetAsync(Guid examId, CancellationToken ct);
    Task<IReadOnlyList<PersonalSchedule>> MyScheduleDetailsAsync(CancellationToken ct);
}

public interface IScheduleReadiness
{
    Task<bool> IsFullyScheduledAsync(Guid examId, CancellationToken ct);
}

public interface IExamScheduleStore
{
    Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct);
    Task<Exam?> FindExamAsync(Guid examId, CancellationToken ct);
    Task<IReadOnlyList<ScheduleEnrollment>> ListRosterAsync(Guid examId, CancellationToken ct);
    Task LockSchedulingAsync(CancellationToken ct);
    Task<Exam> LockExamAsync(Guid examId, CancellationToken ct);
    Task ValidateAndStageAsync(Exam exam, IReadOnlyList<ScheduleSlot> slots, CancellationToken ct);
    Task<IReadOnlyList<PersonalSchedule>> MyScheduleAsync(Guid studentId, CancellationToken ct);
}
