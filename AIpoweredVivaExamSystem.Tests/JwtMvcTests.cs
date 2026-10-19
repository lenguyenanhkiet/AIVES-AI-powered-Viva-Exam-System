using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class JwtMvcTests
{
    private const string Key = "mvc-tests-only-signing-key-with-at-least-sixty-four-bytes-1234567890";

    [Fact]
    public async Task Issued_token_authenticates_expected_identity_and_expiry()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetService<IAccessTokenIssuer>();
        Assert.NotNull(issuer);
        var user = new AuthenticatedUser(Guid.NewGuid(), "jwt@example.com", "JWT test user");
        var token = issuer.Issue(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        var response = await client.GetAsync("/_tests/jwt");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(user.Id.ToString(), json.RootElement.GetProperty("id").GetString());
        Assert.Equal(user.Email, json.RootElement.GetProperty("email").GetString());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Value);
        Assert.Equal(token.ExpiresAt.ToUnixTimeSeconds(), long.Parse(jwt.Claims.Single(c => c.Type == "exp").Value));
        Assert.InRange(token.ExpiresAt - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(61));
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "PasswordHash" || c.Type == "role");
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("algorithm")]
    [InlineData("missing-expiry")]
    [InlineData("malformed")]
    [InlineData("missing")]
    public async Task Bearer_scheme_rejects_invalid_tokens(string fault)
    {
        await using var factory = Factory();
        using var client = Client(factory);
        if (fault != "missing") client.DefaultRequestHeaders.Authorization = new("Bearer", Token(fault));
        var response = await client.GetAsync("/_tests/jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_jwt_does_not_replace_MVC_cookie_session()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token("valid"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/_tests/jwt")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
    }

    [Theory]
    [InlineData("Jwt:Key", "short")]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:ExpirationMinutes", "0")]
    [InlineData("Jwt:ExpirationMinutes", "1441")]
    public void Invalid_jwt_configuration_fails_at_startup(string setting, string value)
    {
        using var factory = Factory(setting, value);
        Assert.Throws<OptionsValidationException>(() => Client(factory));
    }

    // Probe chỉ được thêm bởi factory test, không tồn tại trong route của Web khi chạy thật.
    private static WebApplicationFactory<HomeController> Factory(string? setting = null, string? value = null) =>
        new WebApplicationFactory<HomeController>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Testing");
            web.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = Key, ["Jwt:Issuer"] = "Aives.Tests",
                    ["Jwt:Audience"] = "Aives.Tests.Client", ["Jwt:ExpirationMinutes"] = "60"
                };
                if (setting is not null) settings[setting] = value;
                configuration.AddInMemoryCollection(settings);
            });
            web.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(JwtProbeController).Assembly));
        });

    private static HttpClient Client(WebApplicationFactory<HomeController> factory) => factory.CreateClient(new()
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    // Tự tạo dữ liệu sai độc lập với issuer sản phẩm để phát hiện validator bị nới lỏng.
    private static string Token(string fault)
    {
        if (fault == "malformed") return "not-a-jwt";
        var now = DateTime.UtcNow;
        var signingKey = fault == "signature" ? "different-tests-signing-key-32-bytes-minimum" : Key;
        var jwt = new JwtSecurityToken(
            issuer: fault == "issuer" ? "Wrong.Issuer" : "Aives.Tests",
            audience: fault == "audience" ? "Wrong.Audience" : "Aives.Tests.Client",
            claims: [new Claim("sub", "test-id"), new Claim("email", "jwt@example.com")],
            notBefore: now.AddMinutes(-10),
            expires: fault == "missing-expiry" ? null : fault == "expired" ? now.AddSeconds(-1) : now.AddMinutes(5),
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                fault == "algorithm" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}

[Authorize(AuthenticationSchemes = "Bearer")]
[Route("/_tests/jwt")]
public sealed class JwtProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { id = User.FindFirst("sub")?.Value, email = User.FindFirst("email")?.Value });
}
