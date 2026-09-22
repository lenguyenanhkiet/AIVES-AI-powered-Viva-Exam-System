using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;
namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class LoginUserRepository(ApplicationDbContext db) : ILoginUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        // Chỉ đọc, loại tài khoản đã xóa mềm. Email dùng collation của unique index hiện tại.
        return db.Users.AsNoTracking().SingleOrDefaultAsync(
            user => user.Email == email && user.DeletedAt == null, cancellationToken);
    }
}
