using AIpoweredVivaExamSystem.DataAccess.Contracts.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.VivaSessions;

public interface IVivaSessionStore
{
    Task<StudentSession> GetOrCreateAsync(Guid enrollmentId, CancellationToken ct);
    Task<Guid> FindEnrollmentAsync(Guid sessionId, CancellationToken ct);
    Task CompleteAsync(Guid sessionId, CancellationToken ct);
}
