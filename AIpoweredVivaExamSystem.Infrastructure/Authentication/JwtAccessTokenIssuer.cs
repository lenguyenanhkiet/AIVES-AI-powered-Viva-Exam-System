using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AIpoweredVivaExamSystem.Application.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AIpoweredVivaExamSystem.Infrastructure.Authentication;

/// <summary>Tạo JWT HS256 cho consumer đã xác thực; tách khỏi ticket cookie của MVC.</summary>
public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : IAccessTokenIssuer
{
    public AccessToken Issue(AuthenticatedUser user)
    {
        var settings = options.Value;
        // JWT dùng giây Unix: làm tròn để ExpiresAt trả về khớp chính xác claim exp.
        var now = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds());
        var expires = now.AddMinutes(settings.ExpirationMinutes);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, [
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email), new Claim("name", user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        ], now.UtcDateTime, expires.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        // Không đưa PasswordHash hoặc role giả vào token; role sẽ lấy từ module UserRole khi được tích hợp.
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
