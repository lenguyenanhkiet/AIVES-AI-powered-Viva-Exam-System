using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.ExamStudents;

/// <summary>Roster persistence only; the application service owns authorization and business rules.</summary>
public interface IExamRosterStore
{
    Task<IReadOnlyList<RosterEntry>> ListAsync(Guid examId, CancellationToken ct);
    Task<ExamSummary?> FindExamSummaryAsync(Guid examId, CancellationToken ct);
    // Called inside IExamUnitOfWork. SQL implementations lock exam first, matching session lock order.
    Task<Exam> LockExamAsync(Guid examId, CancellationToken ct);
    Task<User?> FindStudentAsync(string normalizedEmail, CancellationToken ct);
    Task<bool> ContainsAsync(Guid examId, Guid studentId, CancellationToken ct);
    Task<ExamStudent?> FindEnrollmentAsync(Guid examId, Guid enrollmentId, CancellationToken ct);
    Task<bool> HasSessionAsync(Guid enrollmentId, CancellationToken ct);
    void Add(ExamStudent enrollment);
    void Remove(ExamStudent enrollment);
}
