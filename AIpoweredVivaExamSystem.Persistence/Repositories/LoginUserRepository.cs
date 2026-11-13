using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

/// <summary>Đọc Users từ SQL thật cho LoginService; không tracking vì Login không cập nhật tài khoản.</summary>
public sealed class LoginUserRepository(ApplicationDbContext context) : ILoginUserRepository, IRealtimeUserReader
{
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<RealtimeUser?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await FindByIdAsync(id, cancellationToken);
        return user is null ? null : new RealtimeUser(user.Id,
            (AIpoweredVivaExamSystem.Application.Contracts.UserRole)user.Role,
            (AIpoweredVivaExamSystem.Application.Contracts.UserStatus)user.Status,
            user.DeletedAt is not null);
    }
}
