using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AIpoweredVivaExamSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class JwtAuthenticationTests
{
    [Fact]
    public async Task Login_token_authenticates_the_same_user_and_honors_configured_expiry()
    {
        using var app = new LoginApiTests.LoginFactory();
        var user = await app.AddUserAsync(UserStatus.Active);
        using var client = app.CreateClient();
        var before = DateTimeOffset.UtcNow;
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "Long-Test-Password!2026" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        var expiry = body.GetProperty("expiresAtUtc").GetDateTimeOffset();
        Assert.InRange(expiry, before.AddMinutes(7).AddSeconds(-1), DateTimeOffset.UtcNow.AddMinutes(7));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(expiry.ToUnixTimeSeconds(), jwt.Payload.Expiration);
        Assert.Equal("aives-tests", jwt.Issuer);
        Assert.Contains("aives-test-client", jwt.Audiences);
        Assert.False(string.IsNullOrEmpty(jwt.Id));
        Assert.DoesNotContain(jwt.Claims, c => c.Type is "role" or "password" or "PasswordHash");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var protectedResponse = await client.GetAsync("/_tests/identity");
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
        var identity = await protectedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.Id.ToString(), identity.GetProperty("id").GetString());
        Assert.Equal(user.Email, identity.GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/_tests/admin")).StatusCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("future")]
    public async Task Protected_endpoint_rejects_invalid_tokens(string scenario)
    {
        using var app = new LoginApiTests.LoginFactory();
        using var client = app.CreateClient();
        if (scenario != "missing")
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scenario == "malformed" ? "not-a-jwt" : CreateToken(scenario));
        var response = await client.GetAsync("/_tests/identity");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Theory]
    [InlineData("Jwt:Key", "")]
    [InlineData("Jwt:Key", "too-short")]
    [InlineData("Jwt:Issuer", " ")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:ExpirationMinutes", "0")]
    [InlineData("Jwt:ExpirationMinutes", "-1")]
    public void Invalid_configuration_fails_at_startup(string key, string value)
    {
        using var app = new LoginApiTests.LoginFactory(new() { [key] = value });
        Assert.Throws<OptionsValidationException>(() => app.CreateClient());
    }

    [Fact]
    public async Task Swagger_exposes_bearer_authorization()
    {
        using var app = new LoginApiTests.LoginFactory();
        using var client = app.CreateClient();
        var spec = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var bearer = spec.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.True(spec.GetProperty("security")[0].TryGetProperty("Bearer", out _));
        // Login phải dùng được trước khi có token, kể cả theo hợp đồng OpenAPI.
        Assert.Equal(0, spec.GetProperty("paths").GetProperty("/api/v1/auth/login")
            .GetProperty("post").GetProperty("security").GetArrayLength());
    }

    // Token được tạo độc lập để test cấu hình validator, không dùng issuer của production.
    private static string CreateToken(string scenario)
    {
        var now = DateTime.UtcNow;
        var key = scenario == "signature" ? new string('X', 48) : LoginApiTests.LoginFactory.TestKey;
        var jwt = new JwtSecurityToken(
            issuer: scenario == "issuer" ? "wrong-issuer" : "aives-tests",
            audience: scenario == "audience" ? "wrong-audience" : "aives-test-client",
            claims: [new Claim("sub", Guid.NewGuid().ToString())],
            notBefore: scenario == "future" ? now.AddMinutes(2) : now.AddMinutes(-2),
            expires: scenario == "expired" ? now.AddSeconds(-10) : now.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}

[ApiController]
[Authorize]
public sealed class JwtProbeController : ControllerBase
{
    [HttpGet("/_tests/identity")]
    public object Identity() => new { id = User.FindFirstValue("sub"), email = User.FindFirstValue("email") };

    [HttpGet("/_tests/admin")]
    [Authorize(Roles = "ADMIN")]
    public IActionResult Admin() => Ok();
}
