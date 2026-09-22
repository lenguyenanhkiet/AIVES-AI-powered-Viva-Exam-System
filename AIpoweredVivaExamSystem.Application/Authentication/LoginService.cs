using AIpoweredVivaExamSystem.Domain.Enums;
namespace AIpoweredVivaExamSystem.Application.Authentication;

public sealed class LoginService(ILoginUserRepository users, IPasswordVerifier passwords)
{
    public async Task<LoginResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email, cancellationToken);
        // Dùng cùng kết quả thất bại cho tài khoản không tồn tại, sai mật khẩu hoặc bị khóa.
        // Không trim mật khẩu vì khoảng trắng có thể là một phần mật khẩu.
        if (user is null || !passwords.Verify(user, password) || user.Status != UserStatus.Active)
            return null;

        // Branch Login chỉ xác minh tài khoản; phát JWT thuộc task tiếp theo.
        return new LoginResponse(user.Id, user.Email, user.FullName);
    }
}
