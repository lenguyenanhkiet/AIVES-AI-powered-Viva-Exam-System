using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.Authentication;

/// <summary>Xử lý nghiệp vụ Login, dùng chung cho MVC và các consumer JWT sau này.</summary>
public sealed class LoginService(ILoginUserRepository users, IPasswordVerifier passwords)
{
    /// <summary>Chuẩn hóa email giống Register, giữ nguyên mật khẩu, từ chối tài khoản không hoạt động.</summary>
    public async Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return null;
        var user = await users.FindByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken);
        // Tất cả trường hợp thất bại trả null: controller không tiết lộ email có tồn tại hay không.
        if (user is null || user.Status != UserStatus.Active || user.DeletedAt is not null
            || !passwords.Verify(user, password)) return null;
        return new(user.Id, user.Email, user.FullName);
    }
}
