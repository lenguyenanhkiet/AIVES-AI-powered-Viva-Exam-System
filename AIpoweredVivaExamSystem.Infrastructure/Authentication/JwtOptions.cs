namespace AIpoweredVivaExamSystem.Infrastructure.Authentication;

/// <summary>Đọc section Jwt từ cấu hình local hoặc biến môi trường; key không được commit.</summary>
public sealed class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int ExpirationMinutes { get; set; } = 60;
}
