namespace AIpoweredVivaExamSystem.DataAccess.Models.Authentication;

public sealed record AuthenticatedUser(Guid Id, string Email, string FullName,
    string Role = "STUDENT");
