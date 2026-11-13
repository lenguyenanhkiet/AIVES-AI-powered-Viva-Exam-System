using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.ExamStudents;

public interface IExamRosterStore
{
    Task<RosterExamPage> ListManagedAsync(int page, int pageSize, CancellationToken ct);
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
