using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.Authentication;

// Application yêu cầu token qua interface, không phụ thuộc thư viện JWT.
public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);
