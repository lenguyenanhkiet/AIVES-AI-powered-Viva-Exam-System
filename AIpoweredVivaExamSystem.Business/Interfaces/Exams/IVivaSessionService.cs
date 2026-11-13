using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.Business.Exams;

public interface IVivaSessionService
{
    Task<StudentSession> GetOrCreateMySessionAsync(Guid enrollmentId, CancellationToken ct);
    Task CompleteMySessionAsync(Guid sessionId, CancellationToken ct);
}
