using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class LoginApiTests
{
    private const string Password = "Long-Test-Password!2026";

    [Fact]
    public async Task Valid_credentials_return_only_public_user_fields()
    {
        using var app = new LoginFactory();
        var user = await app.AddUserAsync(UserStatus.Active);
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.Id, body.GetProperty("id").GetGuid());
        Assert.Equal(user.Email, body.GetProperty("email").GetString());
        Assert.Equal(user.FullName, body.GetProperty("fullName").GetString());
        Assert.False(body.TryGetProperty("passwordHash", out _));
        Assert.False(body.TryGetProperty("password", out _));
    }

    [Theory]
    [InlineData("long@example.com", "incorrect")]
    [InlineData("missing@example.com", Password)]
    public async Task Invalid_credentials_return_same_generic_unauthorized(string email, string password)
    {
        using var app = new LoginFactory();
        await app.AddUserAsync(UserStatus.Active);
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid email or password.", body.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData(UserStatus.Inactive)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.Suspended)]
    public async Task Non_active_user_cannot_log_in(UserStatus status)
    {
        using var app = new LoginFactory();
        await app.AddUserAsync(status);
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "long@example.com", password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData("not-an-email", Password)]
    [InlineData("long@example.com", "")]
    [InlineData(null, Password)]
    public async Task Invalid_request_returns_bad_request(string? email, string password)
    {
        using var app = new LoginFactory();
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deleted_user_cannot_log_in()
    {
        using var app = new LoginFactory();
        await app.AddUserAsync(UserStatus.Active, deleted: true);
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "long@example.com", password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_stored_hash_is_rejected_without_server_error()
    {
        using var app = new LoginFactory();
        await app.AddUserAsync(UserStatus.Active, malformedHash: true);
        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "long@example.com", password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Mỗi test dùng database SQLite riêng, không chạm vào SQL Server của nhóm.
    public sealed class LoginFactory(Dictionary<string, string?>? overrides = null) : WebApplicationFactory<Program>
    {
        public const string TestKey = "Test-only-signing-key-with-at-least-32-bytes-2026";
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = TestKey,
                    ["Jwt:Issuer"] = "aives-tests",
                    ["Jwt:Audience"] = "aives-test-client",
                    ["Jwt:ExpirationMinutes"] = "7"
                };
                if (overrides is not null)
                    foreach (var pair in overrides) settings[pair.Key] = pair.Value;
                config.AddInMemoryCollection(settings);
            });
            ClientOptions.BaseAddress = new Uri("https://localhost");
            connection.Open();
            builder.ConfigureServices(services =>
            {
                // Endpoint bảo vệ chỉ tồn tại trong assembly test, không đưa vào API thật.
                services.AddControllers().AddApplicationPart(typeof(JwtProbeController).Assembly);
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            });
        }

        public async Task<User> AddUserAsync(UserStatus status, bool deleted = false, bool malformedHash = false)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = new User("long@example.com", "", "Long", status);
            // Tạo hash thật theo cùng quy ước đăng ký; không giả lập bước verify.
            user.ChangePassword(malformedHash ? "invalid-hash" : new PasswordHasher<User>().HashPassword(user, Password));
            if (deleted) user.DeletedAt = DateTimeOffset.UtcNow;
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) connection.Dispose();
        }
    }
}
