using AIpoweredVivaExamSystem.Domain.Enums;
namespace AIpoweredVivaExamSystem.Application.Authentication;

public sealed class LoginService(ILoginUserRepository users, IPasswordVerifier passwords, IAccessTokenIssuer tokens)
{
    public async Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email, cancellationToken);
        // Dùng cùng kết quả thất bại cho tài khoản không tồn tại, sai mật khẩu hoặc bị khóa.
        // Không trim mật khẩu vì khoảng trắng có thể là một phần mật khẩu.
        if (user is null || !passwords.Verify(user, password) || user.Status != UserStatus.Active)
            return null;

        // Chỉ phát token sau khi mật khẩu và trạng thái user đã được xác minh.
        var token = tokens.Issue(user);
        return new LoginResponse(user.Id, user.Email, user.FullName, token.Value, token.ExpiresAtUtc);
    }
}
