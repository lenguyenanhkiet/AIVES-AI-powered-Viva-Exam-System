namespace AIpoweredVivaExamSystem.DataAccess.Models.Authentication;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>Consumer JWT gọi sau LoginService.AuthenticateAsync; MVC tiếp tục dùng cookie riêng.</summary>
