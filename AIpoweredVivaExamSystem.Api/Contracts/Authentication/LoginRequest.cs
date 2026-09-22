using System.ComponentModel.DataAnnotations;
namespace AIpoweredVivaExamSystem.Api.Contracts.Authentication;

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(255)]
    public string Email { get; init; } = string.Empty;

    // Kiểm tra độ mạnh thuộc đăng ký; Login chỉ yêu cầu mật khẩu có giá trị.
    [Required]
    public string Password { get; init; } = string.Empty;
}
