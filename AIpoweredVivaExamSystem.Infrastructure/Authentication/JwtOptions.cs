namespace AIpoweredVivaExamSystem.Infrastructure.Authentication;

// Cùng một bộ cấu hình được dùng để phát và kiểm tra token.
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; }
}
