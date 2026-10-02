using System.Text;
using AIpoweredVivaExamSystem.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AIpoweredVivaExamSystem.Web.Authentication;

/// <summary>Presentation chọn cookie cho MVC; named scheme Bearer chỉ dùng khi endpoint yêu cầu rõ ràng.</summary>
public static class AuthConfiguration
{
    public static IServiceCollection AddAivesAuthentication(this IServiceCollection services)
    {
        services.AddScoped<MvcCookieEvents>();
        // BindConfiguration đọc IConfiguration qua DI, nên cấu hình môi trường/test được áp dụng trước khi validate.
        services.AddOptions<JwtOptions>().BindConfiguration("Jwt")
            .Validate(options => Encoding.UTF8.GetByteCount(options.Key) >= 32, "Jwt:Key must contain at least 32 UTF-8 bytes.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
            .Validate(options => options.ExpirationMinutes is >= 1 and <= 1440, "Jwt:ExpirationMinutes must be between 1 and 1440.")
            .ValidateOnStart();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "AIVES.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                // Cho phép chạy HTTP local/Docker; trên HTTPS cookie luôn có cờ Secure.
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Home/StatusCode/403";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                options.SlidingExpiration = false;
                options.EventsType = typeof(MvcCookieEvents);
            })
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var settings = jwt.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new()
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true, ValidIssuer = settings.Issuer,
                    ValidateAudience = true, ValidAudience = settings.Audience,
                    ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                    ClockSkew = TimeSpan.Zero, NameClaimType = "name", RoleClaimType = "role"
                };
            });

        // Bảo vệ cả MVC controller mới chưa gắn [Authorize]; Login và trang lỗi có [AllowAnonymous].
        services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().Build());
        return services;
    }
}
