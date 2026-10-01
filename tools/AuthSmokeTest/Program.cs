using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Infrastructure;
using AIpoweredVivaExamSystem.Infrastructure.Authentication;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

// Smoke test chạy ngoài TestServer: gửi HTTP thật tới Web đang chạy và đọc Users từ SQL local thật.
// Credentials chỉ nằm trong bộ nhớ. finally chỉ xóa đúng tài khoản có Guid vừa tạo, không đụng dữ liệu khác.
var webPath = Path.GetFullPath(Value("--web-path") ?? "AIpoweredVivaExamSystem.Web");
var baseUri = new Uri(Value("--base-url") ?? "http://127.0.0.1:5088");
if (!baseUri.IsLoopback) throw new InvalidOperationException("Smoke test is limited to a local Web URL.");
var configuration = new ConfigurationBuilder().SetBasePath(webPath)
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables().Build();
var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing local SQL connection configuration.");
var sql = new SqlConnectionStringBuilder(connectionString);
var server = sql.DataSource.Split('\\', ',')[0];
if (!new[] { Environment.MachineName, "localhost", "127.0.0.1", ".", "(local)", "(localdb)" }
    .Contains(server, StringComparer.OrdinalIgnoreCase))
    throw new InvalidOperationException("Smoke test is limited to a local SQL Server.");

await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connectionString).Options);
var pending = (await context.Database.GetPendingMigrationsAsync()).ToArray();
var loginOnly = args.Contains("--login-only");
if (pending.Length > 0)
{
    Console.WriteLine("Pending migrations: " + string.Join(", ", pending));
    if (loginOnly)
        Console.WriteLine("Login-only mode: preserving existing schema; skipping dashboard/subject SQL queries.");
    else
    {
        if (!args.Contains("--apply-migrations")) throw new InvalidOperationException("Pending migrations. Review them and rerun with --apply-migrations, or use --login-only.");
        // Chỉ áp dụng migration đã có trên main khi được chọn rõ ràng; không xóa DB của người dùng.
        await context.Database.MigrateAsync();
        Console.WriteLine($"Applied {pending.Length} existing migrations to local DB.");
    }
}

var email = "long-smoke-" + Guid.NewGuid().ToString("N") + "@example.com";
var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var user = new User(email, "", "Long Local Smoke", UserStatus.Active);
user.ChangePassword(new PasswordHasher<User>().HashPassword(user, password));
var seeded = false;
try
{
    context.Users.Add(user);
    await context.SaveChangesAsync();
    seeded = true;
    Console.WriteLine($"SQL connected: {sql.DataSource} / {sql.InitialCatalog}; temporary account created.");
    using var client = Browser();
    Check((await client.GetAsync("/Subjects")).StatusCode == HttpStatusCode.Redirect, "anonymous business page redirects to Login");
    Check((await client.GetAsync("/css/site.css")).StatusCode == HttpStatusCode.OK, "anonymous static CSS is accessible");

    using (var wrongBrowser = Browser())
    {
        var wrong = await Login(wrongBrowser, "incorrect-password");
        Check(wrong.StatusCode == HttpStatusCode.OK, "wrong password returns the form");
        Check((await wrongBrowser.GetAsync("/Account/Profile")).StatusCode == HttpStatusCode.Redirect, "wrong password creates no session");
    }
    var login = await Login(client, password);
    Check(login.StatusCode == HttpStatusCode.Redirect && login.Headers.Location?.OriginalString == "/Account/Profile", "valid password creates MVC session");
    var profile = await client.GetStringAsync("/Account/Profile");
    Check(profile.Contains(email), "profile identity matches account stored in SQL");
    if (!loginOnly)
    {
        Check((await client.GetAsync("/")).StatusCode == HttpStatusCode.OK, "authenticated dashboard queries local DB");
        Check((await client.GetAsync("/Subjects")).StatusCode == HttpStatusCode.OK, "authenticated subject page queries local DB");
    }
    Check((await client.PostAsync("/Account/Logout", new FormUrlEncodedContent([]))).StatusCode == HttpStatusCode.BadRequest, "logout rejects missing CSRF token");

    // Gọi LoginService/repository/issuer thật với cùng DB và cấu hình; token không được đưa ra console.
    var services = new ServiceCollection();
    services.AddInfrastructure();
    services.AddOptions<JwtOptions>().Bind(configuration.GetSection("Jwt"));
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var authenticated = await new LoginService(new AIpoweredVivaExamSystem.Persistence.Repositories.LoginUserRepository(context),
        scope.ServiceProvider.GetRequiredService<IPasswordVerifier>()).AuthenticateAsync(email, password, default);
    Check(authenticated?.Id == user.Id, "LoginService verifies real SQL account");
    var token = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>().Issue(authenticated!);
    var jwt = configuration.GetSection("Jwt").Get<JwtOptions>()!;
    var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token.Value, new()
    {
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience, ValidateLifetime = true, ClockSkew = TimeSpan.Zero
    }, out _);
    Check(principal.FindFirst("sub")?.Value == user.Id.ToString(), "JWT signature, issuer, audience, expiry and SQL identity are valid");

    var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
    { ["__RequestVerificationToken"] = Token(profile) }));
    Check(logout.StatusCode == HttpStatusCode.Redirect, "logout succeeds with CSRF token");
    Check((await client.GetAsync("/Account/Profile")).StatusCode == HttpStatusCode.Redirect, "logged-out browser cannot open profile");
}
finally
{
    if (seeded)
    {
        var removed = await context.Users.Where(candidate => candidate.Id == user.Id && candidate.Email == email).ExecuteDeleteAsync();
        Check(removed == 1, "temporary smoke account removed from local DB");
    }
}
Console.WriteLine(loginOnly ? "SMOKE PASS: MVC Login/Logout and JWT verified on current local SQL schema."
    : "SMOKE PASS: MVC Login/Logout, real SQL queries and JWT verified.");

string? Value(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

HttpClient Browser() => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
{ BaseAddress = baseUri };

static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
    "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

async Task<HttpResponseMessage> Login(HttpClient client, string suppliedPassword)
{
    var html = await client.GetStringAsync("/Account/Login");
    return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["Email"] = email, ["Password"] = suppliedPassword, ["ReturnUrl"] = "/Account/Profile",
        ["__RequestVerificationToken"] = Token(html)
    }));
}

static void Check(bool condition, string purpose)
{
    if (!condition) throw new InvalidOperationException("SMOKE FAIL: " + purpose);
    Console.WriteLine("PASS: " + purpose);
}
