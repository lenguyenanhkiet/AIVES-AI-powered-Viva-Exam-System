using AIpoweredVivaExamSystem.Application.Contracts;

namespace AIpoweredVivaExamSystem.Application.Authentication;

public sealed record RealtimeUser(Guid Id, UserRole Role, UserStatus Status, bool IsDeleted);

public interface IRealtimeUserReader
{
    Task<RealtimeUser?> FindAsync(Guid id, CancellationToken cancellationToken);
}
