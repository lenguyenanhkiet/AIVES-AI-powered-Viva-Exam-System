using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.Business.Authentication;

public sealed class AccessService(IAccessStore store, ICurrentUser actor)
{
    public Task<AccessOverview> OverviewAsync(CancellationToken ct) { RequireAdmin(); return store.OverviewAsync(ct); }
    public async Task AssignRoleAsync(Guid userId, UserRole role, CancellationToken ct)
    {
        RequireAdmin();
        if (userId == actor.Id && role != UserRole.ADMIN)
            throw new ResourceConflictException("You cannot remove your own administrator role.");
        var user = await store.FindUserAsync(userId, ct) ?? throw new ResourceNotFoundException("User not found.");
        user.AssignRole(role); await store.SaveAsync(ct);
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
