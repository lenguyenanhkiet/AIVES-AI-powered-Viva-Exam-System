using AIpoweredVivaExamSystem.Domain.Entities;
namespace AIpoweredVivaExamSystem.Application.Authentication;

// Application chỉ biết hợp đồng truy vấn, không phụ thuộc EF Core.
public interface ILoginUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}
