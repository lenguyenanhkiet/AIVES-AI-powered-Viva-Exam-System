using AIpoweredVivaExamSystem.Business.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace AIpoweredVivaExamSystem.Business.Integrations.Authentication;

/// <summary>Dùng PasswordHasher&lt;User&gt; giống branch Register của Minh; salt nằm trong hash.</summary>
public sealed class IdentityPasswordVerifier(IPasswordHasher<User> hasher) : IPasswordVerifier
{
    public bool Verify(User user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash)) return false;
        try
        {
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            // Hash cần nâng cấp vẫn xác thực được. Việc ghi hash mới thuộc module quản lý tài khoản.
            return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            // Dữ liệu hash hỏng phải bị từ chối, không làm trang Login trả lỗi 500.
            return false;
        }
    }
}
