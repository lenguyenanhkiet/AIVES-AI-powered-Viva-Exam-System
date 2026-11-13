using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Contracts.Authentication;

public interface IAccessStore
{
    Task<AccessOverview> OverviewAsync(CancellationToken ct);
    Task<User?> FindUserAsync(Guid id, CancellationToken ct);
    Task<bool> SubjectExistsAsync(Guid id, CancellationToken ct);
    Task<bool> AssignmentExistsAsync(Guid lecturerId, Guid subjectId, CancellationToken ct);
    void AddAssignment(LecturerSubject assignment);
    Task RemoveAssignmentAsync(Guid id, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
