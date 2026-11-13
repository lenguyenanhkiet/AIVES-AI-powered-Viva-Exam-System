using AIpoweredVivaExamSystem.DataAccess.Contracts.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.DataAccess.Repositories;

/// <summary>Đọc Users từ SQL thật cho LoginService; không tracking vì Login không cập nhật tài khoản.</summary>
public sealed class LoginUserRepository(ApplicationDbContext context) : ILoginUserRepository
{
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
}
