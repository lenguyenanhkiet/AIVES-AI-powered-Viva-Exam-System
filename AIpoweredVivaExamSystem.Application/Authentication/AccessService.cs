using AIpoweredVivaExamSystem.Application.Common;
using ContractUserRole = AIpoweredVivaExamSystem.Application.Contracts.UserRole;
using DomainUserRole = AIpoweredVivaExamSystem.Domain.Enums.UserRole;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.Authentication;

public sealed record AccessUser(Guid Id, string Email, string FullName, ContractUserRole Role);
public sealed record AccessSubject(Guid Id, string Code);
public sealed record SubjectAssignment(Guid Id, string LecturerEmail, string SubjectCode);
public sealed record AccessOverview(IReadOnlyList<AccessUser> Users, IReadOnlyList<AccessSubject> Subjects,
    IReadOnlyList<SubjectAssignment> Assignments);
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
public sealed class AccessService(IAccessStore store, ICurrentUser actor)
{
    public Task<AccessOverview> OverviewAsync(CancellationToken ct) { RequireAdmin(); return store.OverviewAsync(ct); }
    public async Task AssignRoleAsync(Guid userId, ContractUserRole role, CancellationToken ct)
    {
        RequireAdmin();
        if (!Enum.IsDefined(role)) throw new ResourceConflictException("Choose a valid role.");
        if (userId == actor.Id && role != ContractUserRole.ADMIN)
            throw new ResourceConflictException("You cannot remove your own administrator role.");
        var user = await store.FindUserAsync(userId, ct) ?? throw new ResourceNotFoundException("User not found.");
        user.AssignRole((DomainUserRole)role); await store.SaveAsync(ct);
    }
    public async Task AssignSubjectAsync(Guid lecturerId, Guid subjectId, CancellationToken ct)
    {
        RequireAdmin();
        var user = await store.FindUserAsync(lecturerId, ct);
        if (user?.Role != UserRole.LECTURER || user.Status != UserStatus.Active || user.DeletedAt is not null)
            throw new ResourceConflictException("Choose an active lecturer.");
        if (!await store.SubjectExistsAsync(subjectId, ct)) throw new ResourceNotFoundException("Subject not found.");
        if (!await store.AssignmentExistsAsync(lecturerId, subjectId, ct))
        { store.AddAssignment(new(lecturerId, subjectId)); await store.SaveAsync(ct); }
    }
    public async Task UnassignAsync(Guid id, CancellationToken ct)
    { RequireAdmin(); await store.RemoveAssignmentAsync(id, ct); await store.SaveAsync(ct); }
    private void RequireAdmin()
    { if (!actor.IsAdministrator) throw new UnauthorizedAccessException("Administrator access is required."); }
}
