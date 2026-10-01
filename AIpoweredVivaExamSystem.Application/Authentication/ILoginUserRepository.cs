using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.Authentication;

/// <summary>Ranh giới truy vấn tài khoản cho Login; Persistence chịu trách nhiệm EF/SQL.</summary>
public interface ILoginUserRepository
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
}
