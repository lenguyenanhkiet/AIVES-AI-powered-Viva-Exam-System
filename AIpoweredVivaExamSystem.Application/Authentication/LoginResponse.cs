namespace AIpoweredVivaExamSystem.Application.Authentication;

// Chỉ trả dữ liệu công khai, không serialize User chứa PasswordHash.
public sealed record LoginResponse(Guid Id, string Email, string FullName);
