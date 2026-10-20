using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Entities;
namespace AIpoweredVivaExamSystem.Application.VivaSessions;

public enum SessionState { NotStarted, InProgress, Completed }

public sealed record LiveStudent(Guid EnrollmentId, string Name, DateTimeOffset? ScheduledAt, SessionState SessionStatus);
public sealed record ExamLiveStatus(ExamStatus Status, ExamFoundationCounts Counts, int CompletedSessions, IReadOnlyList<LiveStudent> Roster);
public interface IExamLiveReader
{
    Task<ExamSummary> SummaryAsync(Guid examId, CancellationToken ct);
    Task<ExamLiveStatus> ReadAsync(Guid examId, CancellationToken ct);
}
