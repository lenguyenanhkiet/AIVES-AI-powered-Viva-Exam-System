using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Domain.Entities;
using Microsoft.AspNetCore.Identity;
namespace AIpoweredVivaExamSystem.Infrastructure.Authentication;

public sealed class IdentityPasswordVerifier(IPasswordHasher<User> hasher) : IPasswordVerifier
{
    public bool Verify(User user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash)) return false;
        try
        {
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            // Hash cũ vẫn hợp lệ; nâng cấp hash cần phối hợp module quản lý tài khoản.
            return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            // Hash sai định dạng bị từ chối thay vì làm endpoint trả lỗi 500.
            return false;
        }
    }
}
