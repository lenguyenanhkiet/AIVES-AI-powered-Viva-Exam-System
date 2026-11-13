using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;

public interface IExamAccessStore
{
    Task<ExamAccess?> FindExamAsync(Guid examId, CancellationToken ct);
    // Retained access facts, including soft-deleted exams, for list-change notifications only.
    Task<ExamAccess?> FindExamForNotificationAsync(Guid examId, CancellationToken ct);
    Task<ExamStudentAccess?> FindEnrollmentAsync(Guid enrollmentId, CancellationToken ct);
    Task<bool> IsAssignedAsync(Guid lecturerId, Guid subjectId, CancellationToken ct);
    Task<bool> IsEnrolledAsync(Guid examId, Guid studentId, CancellationToken ct);
}
