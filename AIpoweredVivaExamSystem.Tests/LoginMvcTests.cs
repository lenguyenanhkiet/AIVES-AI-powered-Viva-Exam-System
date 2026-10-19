using System.Net;
using System.Text.RegularExpressions;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Web.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

[Collection("SQL Server")]
public sealed class LoginMvcTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Anonymous_user_can_open_login_form()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("__RequestVerificationToken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Anonymous_user_is_redirected_from_business_pages_to_login()
    {
        await using var factory = CreateFactory();
        using var client = Client(factory);
        var response = await client.GetAsync("/Subjects");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Valid_login_normalizes_email_creates_cookie_and_opens_profile()
    {
        var email = await SeedAsync(UserStatus.Active, password: " pass With Spaces ");
        await using var factory = CreateFactory();
        using var client = Client(factory);
        var response = await LoginAsync(client, "  " + email.ToUpperInvariant() + "  ", " pass With Spaces ", "/Account/Profile");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Profile", response.Headers.Location!.OriginalString);
        var cookie = string.Join(";", response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("AIVES.Auth=", cookie);
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("secure", cookie.ToLowerInvariant());
        var profile = await client.GetAsync("/Account/Profile");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var html = await profile.Content.ReadAsStringAsync();
        Assert.Contains(email, html);
        Assert.DoesNotContain("PasswordHash", html);
    }

    [Theory]
    [InlineData(UserStatus.Active, false, "wrong", false)]
    [InlineData(UserStatus.Inactive, false, "Test-password-123!", false)]
    [InlineData(UserStatus.Locked, false, "Test-password-123!", false)]
    [InlineData(UserStatus.Active, true, "Test-password-123!", false)]
    [InlineData(UserStatus.Active, false, "Test-password-123!", true)]
    public async Task Wrong_password_or_unusable_account_cannot_sign_in(UserStatus status, bool deleted, string password, bool badHash)
    {
        var email = await SeedAsync(status, deleted, badHash);
        await using var factory = CreateFactory();
        using var client = Client(factory);
        var response = await LoginAsync(client, email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("AIVES.Auth=")));
        Assert.Contains("Email hoặc mật khẩu không đúng.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
    }

    [Fact]
    public async Task Unknown_email_uses_same_generic_error()
    {
        await using var factory = CreateFactory();
        using var client = Client(factory);
        var response = await LoginAsync(client, "missing-" + Guid.NewGuid() + "@example.com", "Wrong-password");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Email hoặc mật khẩu không đúng.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Invalid_form_shows_validation_without_signing_in()
    {
        await using var factory = CreateFactory();
        using var client = Client(factory);
        var response = await LoginAsync(client, "not-an-email", "");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("field-validation-error", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
    }

    [Fact]
    public async Task Login_rejects_missing_antiforgery_token()
    {
        await using var factory = CreateFactory();
        using var client = Client(factory);
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Email"] = "test@example.com", ["Password"] = "password" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task External_return_url_is_not_followed()
    {
        var email = await SeedAsync(UserStatus.Active);
        await using var factory = CreateFactory();
        using var client = Client(factory);
        var response = await LoginAsync(client, email, "Test-password-123!", "https://example.com/steal");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Logout_requires_antiforgery_and_clears_browser_session()
    {
        var email = await SeedAsync(UserStatus.Active);
        await using var factory = CreateFactory();
        using var client = Client(factory);
        await LoginAsync(client, email, "Test-password-123!");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Logout", new FormUrlEncodedContent([]))).StatusCode);
        var html = await client.GetStringAsync("/Account/Profile");
        var response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = Token(html) }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
    }

    [Fact]
    public async Task Locking_account_revokes_existing_cookie_on_next_request()
    {
        var email = await SeedAsync(UserStatus.Active);
        await using var factory = CreateFactory();
        using var client = Client(factory);
        await LoginAsync(client, email, "Test-password-123!");
        await using var context = fixture.CreateContext();
        var user = await context.Users.SingleAsync(u => u.Email == email);
        user.Lock();
        await context.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Profile")).StatusCode);
    }

    [Fact]
    public async Task Personalized_page_is_not_cached_and_failed_login_does_not_echo_password()
    {
        var email = await SeedAsync(UserStatus.Active);
        await using var factory = CreateFactory();
        using var client = Client(factory);
        var failed = await LoginAsync(client, email, "secret-not-to-echo-123!");
        Assert.DoesNotContain("secret-not-to-echo-123!", await failed.Content.ReadAsStringAsync());
        await LoginAsync(client, email, "Test-password-123!");
        var profile = await client.GetAsync("/Account/Profile");
        Assert.True(profile.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("/Questions")]
    [InlineData("/QuestionImports")]
    [InlineData("/Documents")]
    [InlineData("/Access")]
    [InlineData("/ExamFoundation")]
    public async Task Student_cannot_open_management_pages(string path)
    {
        var email = await SeedAsync(UserStatus.Active);
        await using var factory = CreateFactory();
        using var client = Client(factory);
        await LoginAsync(client, email, "Test-password-123!");
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("403", response.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("/QuestionImports")]
    [InlineData("/Documents")]
    [InlineData("/Access")]
    [InlineData("/ExamFoundation")]
    public async Task Admin_can_render_new_management_forms(string path)
    {
        var email = await SeedAsync(UserStatus.Active, role: UserRole.ADMIN);
        await using var factory = CreateFactory();
        using var client = Client(factory);
        await LoginAsync(client, email, "Test-password-123!");
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("__RequestVerificationToken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Lecturer_can_preview_and_confirm_csv_through_real_mvc_forms()
    {
        var email = await SeedAsync(UserStatus.Active, role: UserRole.LECTURER);
        Guid subjectId;
        string code;
        await using (var db = fixture.CreateContext())
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            var subject = new Subject("CSV"+Guid.NewGuid().ToString("N"), "CSV test subject");
            db.AddRange(subject, new LecturerSubject(user.Id, subject.Id));
            await db.SaveChangesAsync(); subjectId = subject.Id; code = subject.Code;
        }
        await using var factory = CreateFactory();
        using var client = Client(factory);
        await LoginAsync(client, email, "Test-password-123!");
        var form = await client.GetStringAsync("/QuestionImports");
        using var upload = new MultipartFormDataContent();
        upload.Add(new StringContent(Token(form)), "__RequestVerificationToken");
        upload.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes($"SubjectCode,Topic,Content,ExpectedAnswer,BloomLevel,Difficulty\n{code},,Explain MVC.,Model View Controller,Understand,Medium")), "File", "questions.csv");
        var preview = await client.PostAsync("/QuestionImports/Preview", upload);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var html = await preview.Content.ReadAsStringAsync();
        var previewToken = Regex.Match(html, "name=\"token\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(previewToken);
        var confirm = await client.PostAsync("/QuestionImports/Confirm", new FormUrlEncodedContent(new Dictionary<string,string>
        { ["token"] = previewToken, ["__RequestVerificationToken"] = Token(html) }));
        Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
        await using var verify = fixture.CreateContext();
        Assert.Equal(QuestionStatus.Draft, (await verify.Questions.SingleAsync(q => q.SubjectId == subjectId)).Status);
    }

    private sealed class CitedGenerator : AIpoweredVivaExamSystem.Application.QuestionBank.IQuestionGenerator
    {
        public Task<IReadOnlyList<AIpoweredVivaExamSystem.Application.QuestionBank.GeneratedDraft>> GenerateAsync(string instruction,
            BloomLevel bloom, QuestionDifficulty difficulty, int count, IReadOnlyList<DocumentChunk> sources, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AIpoweredVivaExamSystem.Application.QuestionBank.GeneratedDraft>>(
                [new("Explain dependency injection.", "Dependencies are supplied to a class.", sources[0].Id, sources[0].Text)]);
    }

    [Fact]
    public async Task Lecturer_can_upload_pptx_generate_draft_and_view_citation()
    {
        var email = await SeedAsync(UserStatus.Active, role: UserRole.LECTURER);
        Guid subjectId;
        await using (var db = fixture.CreateContext())
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            var subject = new Subject("DOC"+Guid.NewGuid().ToString("N"), "Document test");
            db.AddRange(subject, new LecturerSubject(user.Id, subject.Id));
            await db.SaveChangesAsync(); subjectId = subject.Id;
        }
        var bytes = QuestionBankFileTests.Archive(new Dictionary<string,string>
        {
            ["ppt/presentation.xml"] = "<p:presentation xmlns:p='http://schemas.openxmlformats.org/presentationml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><p:sldIdLst><p:sldId id='256' r:id='first'/></p:sldIdLst></p:presentation>",
            ["ppt/_rels/presentation.xml.rels"] = "<Relationships><Relationship Id='first' Target='slides/slide1.xml'/></Relationships>",
            ["ppt/slides/slide1.xml"] = "<slide xmlns:a='http://schemas.openxmlformats.org/drawingml/2006/main'><a:t>Dependency injection supplies dependencies to a class.</a:t></slide>"
        });
        await using var factory = CreateFactory(new CitedGenerator());
        using var client = Client(factory);
        await LoginAsync(client, email, "Test-password-123!");
        using var upload = new MultipartFormDataContent();
        upload.Add(new StringContent(Token(await client.GetStringAsync("/Documents"))), "__RequestVerificationToken");
        upload.Add(new StringContent(subjectId.ToString()), "SubjectId");
        upload.Add(new ByteArrayContent(bytes), "File", "lecture.pptx");
        var uploaded = await client.PostAsync("/Documents/Upload", upload);
        Assert.Equal(HttpStatusCode.Redirect, uploaded.StatusCode);
        var form = await client.GetStringAsync(uploaded.Headers.Location);
        var docId = Regex.Match(form, "name=\"DocumentId\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(docId);
        var generated = await client.PostAsync("/Documents/Generate", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["DocumentId"] = docId, ["Instruction"] = "dependency injection", ["BloomLevel"] = "Understand",
            ["Difficulty"] = "Medium", ["Count"] = "1", ["__RequestVerificationToken"] = Token(form)
        }));
        Assert.Equal(HttpStatusCode.Redirect, generated.StatusCode);
        await using var verify = fixture.CreateContext();
        var question = await verify.Questions.SingleAsync(q => q.SubjectId == subjectId);
        Assert.Equal(QuestionStatus.Draft, question.Status);
        var details = await client.GetStringAsync("/Questions/Details/" + question.Id);
        Assert.Contains("lecture.pptx", details);
        Assert.Contains("Dependency injection supplies dependencies to a class.", details);
    }

    private async Task<string> SeedAsync(UserStatus status, bool deleted = false, bool badHash = false, string password = "Test-password-123!", UserRole role = UserRole.STUDENT)
    {
        await using var context = fixture.CreateContext();
        var email = "mvc-" + Guid.NewGuid().ToString("N") + "@example.com";
        var user = new User(email, "", "Long MVC Test", status);
        user.AssignRole(role);
        user.ChangePassword(badHash ? "not-a-valid-hash" : new PasswordHasher<User>().HashPassword(user, password));
        if (deleted) user.DeletedAt = DateTimeOffset.UtcNow;
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return email;
    }

    private static HttpClient Client(WebApplicationFactory<HomeController> factory) => factory.CreateClient(new()
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    // Lấy token từ form thật để test cả cookie antiforgery và POST MVC, không tắt CSRF trong test.
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password, string returnUrl = "/")
    {
        var html = await client.GetStringAsync("/Account/Login");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password, ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = Token(html)
        }));
    }

    // Dùng SQL Server thật trong DB test riêng; không thay repository bằng mock.
    private WebApplicationFactory<HomeController> CreateFactory(AIpoweredVivaExamSystem.Application.QuestionBank.IQuestionGenerator? generator = null) => new WebApplicationFactory<HomeController>()
        .WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Testing");
            web.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString,
                    ["Jwt:Key"] = "mvc-tests-only-signing-key-32-bytes-minimum",
                    ["Jwt:Issuer"] = "Aives.Tests", ["Jwt:Audience"] = "Aives.Tests.Client",
                    ["Jwt:ExpirationMinutes"] = "60"
                }));
            // Thay toàn bộ cấu hình DbContext: minimal hosting đăng ký options trước callback này.
            // Test chỉ được phép truy cập DB test riêng của fixture.
            web.ConfigureServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(fixture.ConnectionString));
                if (generator is not null)
                {
                    services.RemoveAll<AIpoweredVivaExamSystem.Application.QuestionBank.IQuestionGenerator>();
                    services.AddSingleton(generator);
                }
            });
        });
}
