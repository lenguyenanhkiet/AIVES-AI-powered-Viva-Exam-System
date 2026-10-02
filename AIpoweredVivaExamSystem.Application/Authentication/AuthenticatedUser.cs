namespace AIpoweredVivaExamSystem.Application.Authentication;

/// <summary>Chỉ trả thông tin cần cho phiên đăng nhập; không đưa PasswordHash ra Presentation.</summary>
public sealed record AuthenticatedUser(Guid Id, string Email, string FullName);
