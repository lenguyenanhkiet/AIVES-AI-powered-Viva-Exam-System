using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.Authentication;

/// <summary>Cho phép Login dùng cùng định dạng hash với Register mà không phụ thuộc thư viện hash.</summary>
public interface IPasswordVerifier
{
    bool Verify(User user, string password);
}
