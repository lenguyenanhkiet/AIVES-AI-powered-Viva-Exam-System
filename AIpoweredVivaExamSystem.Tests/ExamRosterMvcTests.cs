using System.Net;
using System.Text.RegularExpressions;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using AIpoweredVivaExamSystem.Web.Pages.Home;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

[Collection("SQL Server")]
public sealed class ExamRosterMvcTests(SqlServerFixture fixture)
{
    private const string Password = "Roster-test-password-123!";

    private async Task<ExamDemo> SeedAsync()
    {
        await using var db = fixture.CreateContext();
        var d = await ExamDemo.SeedAsync(db);
        d.Lecturer.ChangePassword(new PasswordHasher<User>().HashPassword(d.Lecturer, Password));
        d.Student.ChangePassword(new PasswordHasher<User>().HashPassword(d.Student, Password));
        d.Enrollment.Schedule(new DateTimeOffset(2026, 10, 20, 1, 0, 0, TimeSpan.Zero));
        await db.SaveChangesAsync();
        return d;
    }

    [Fact]
    public async Task ManagerCanAddThreeStudentsSeeEmailAndVietnamTimeAndRemoveOne()
    {
        var d = await SeedAsync();
        var students = Enumerable.Range(1, 3).Select(i => new User(Guid.NewGuid() + "@example.test", "unused", "Roster " + i, UserStatus.Active)).ToArray();
        await using (var db = fixture.CreateContext()) { db.AddRange(students); await db.SaveChangesAsync(); }
        await using var factory = Factory();
        using var client = Client(factory);
        await LoginAsync(client, d.Lecturer.Email);
        var exams = await client.GetStringAsync("/ExamStudents");
        Assert.Contains(d.Exam.Id.ToString(), exams);
        var route = "/ExamStudents/Roster/" + d.Exam.Id;
        foreach (var student in students)
        {
            var form = await client.GetStringAsync(route);
            var response = await client.PostAsync("/ExamStudents/Add/" + d.Exam.Id, Form(Token(form), ("Input.Email", student.Email)));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        var roster = await client.GetStringAsync(route);
        Assert.Contains(d.Student.Email, roster);
        Assert.Contains("20/10/2026 08:00", roster);
        foreach (var student in students) Assert.Contains(student.Email, roster);
        await using var verify = fixture.CreateContext();
        var entry = await verify.ExamStudents.SingleAsync(x => x.ExamId == d.Exam.Id && x.StudentId == students[0].Id);
        var removed = await client.PostAsync("/ExamStudents/Remove/" + d.Exam.Id, Form(Token(roster), ("enrollmentId", entry.Id.ToString())));
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.False(await verify.ExamStudents.AnyAsync(x => x.Id == entry.Id));
        Assert.Equal(3, await verify.ExamStudents.CountAsync(x => x.ExamId == d.Exam.Id));
    }

    [Fact]
    public async Task InvalidAndDuplicateFormsPreserveEmailAndShowErrors()
    {
        var d = await SeedAsync();
        await using var factory = Factory();
        using var client = Client(factory);
        await LoginAsync(client, d.Lecturer.Email);
        var route = "/ExamStudents/Roster/" + d.Exam.Id;
        foreach (var email in new[] { "not-an-email", d.Student.Email, "missing@example.test" })
        {
            var form = await client.GetStringAsync(route);
            var response = await client.PostAsync("/ExamStudents/Add/" + d.Exam.Id, Form(Token(form), ("Input.Email", email)));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("validation-summary-errors", html);
            Assert.Contains(email, html);
        }
        await using var verify = fixture.CreateContext();
        Assert.Single(await verify.ExamStudents.Where(x => x.ExamId == d.Exam.Id).ToArrayAsync());
    }

    [Fact]
    public async Task MutationRoutesRequirePostAndAntiforgery()
    {
        var d = await SeedAsync();
        await using var factory = Factory();
        using var client = Client(factory);
        await LoginAsync(client, d.Lecturer.Email);
        foreach (var action in new[] { "Add", "Remove" })
        {
            var route = "/ExamStudents/" + action + "/" + d.Exam.Id;
            // Mutation pages preserve the existing 404 response for GET requests.
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(route, new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        }
        await using var verify = fixture.CreateContext();
        Assert.True(await verify.ExamStudents.AnyAsync(x => x.Id == d.Enrollment.Id));
    }

    [Fact]
    public async Task AnonymousStudentAndOtherLecturerCannotOpenRoster()
    {
        var d = await SeedAsync();
        var other = await SeedAsync();
        await using var factory = Factory();
        var route = "/ExamStudents/Roster/" + d.Exam.Id;
        using var anonymous = Client(factory);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync(route)).StatusCode);
        using var student = Client(factory);
        await LoginAsync(student, d.Student.Email);
        await AssertDeniedAsync(student, await student.GetAsync(route));
        using var lecturer = Client(factory);
        await LoginAsync(lecturer, other.Lecturer.Email);
        await AssertDeniedAsync(lecturer, await lecturer.GetAsync(route));
        var ownForm = await lecturer.GetStringAsync("/ExamStudents/Roster/" + other.Exam.Id);
        var forbidden = await lecturer.PostAsync("/ExamStudents/Remove/" + d.Exam.Id, Form(Token(ownForm), ("enrollmentId", d.Enrollment.Id.ToString())));
        await AssertDeniedAsync(lecturer, forbidden);
    }

    [Fact]
    public async Task ManagerCanNavigateToOlderExamsThroughRosterPagination()
    {
        var d = await SeedAsync();
        await using (var db = fixture.CreateContext())
        {
            db.AddRange(Enumerable.Range(1, 100).Select(i => new Exam(d.Subject.Id, d.Lecturer.Id,
                "Later exam " + i, d.Exam.StartAt.AddDays(i), d.Exam.EndAt.AddDays(i), 15, 2, 1)));
            await db.SaveChangesAsync();
        }
        await using var factory = Factory();
        using var client = Client(factory);
        await LoginAsync(client, d.Lecturer.Email);
        var html = await client.GetStringAsync("/ExamStudents");
        for (var page = 1; page <= 6 && !html.Contains(d.Exam.Id.ToString()); page++)
        {
            var next = Regex.Match(html, "<a\\b[^>]*rel=\"next\"[^>]*>").Value;
            Assert.NotEmpty(next);
            var href = WebUtility.HtmlDecode(Regex.Match(next, "href=\"([^\"]+)\"").Groups[1].Value);
            Assert.NotEmpty(href);
            html = await client.GetStringAsync(href);
        }
        Assert.Contains(d.Exam.Id.ToString(), html);
    }

    [Fact]
    public async Task DirectRosterLinkWorksForAnExamOutsideTheFirstHundredRows()
    {
        var d = await SeedAsync();
        await using (var db = fixture.CreateContext())
        {
            db.AddRange(Enumerable.Range(1, 100).Select(i => new Exam(d.Subject.Id, d.Lecturer.Id,
                "Later exam " + i, d.Exam.StartAt.AddDays(i), d.Exam.EndAt.AddDays(i), 15, 2, 1)));
            await db.SaveChangesAsync();
        }
        await using var factory = Factory();
        using var client = Client(factory);
        await LoginAsync(client, d.Lecturer.Email);
        var response = await client.GetAsync("/ExamStudents/Roster/" + d.Exam.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(d.Student.Email, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MissingExamFromPageHandlerReturns404()
    {
        var d = await SeedAsync();
        await using var factory = Factory();
        using var client = Client(factory);
        await LoginAsync(client, d.Lecturer.Email);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/ExamStudents/Roster/" + Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task PublishedRosterIsReadOnlyAndRejectsStaleFormSubmission()
    {
        var d = await SeedAsync();
        await using var factory = Factory();
        using var client = Client(factory);
        await LoginAsync(client, d.Lecturer.Email);
        var route = "/ExamStudents/Roster/" + d.Exam.Id;
        var oldForm = await client.GetStringAsync(route);
        await using (var db = fixture.CreateContext())
            await db.Exams.Where(x => x.Id == d.Exam.Id).ExecuteUpdateAsync(x => x.SetProperty(e => e.Status, ExamStatus.Published));
        var html = await client.GetStringAsync(route);
        Assert.DoesNotContain("/ExamStudents/Add/", html);
        Assert.DoesNotContain("/ExamStudents/Remove/", html);
        Assert.Contains(d.Student.Email, html);
        var response = await client.PostAsync("/ExamStudents/Remove/" + d.Exam.Id, Form(Token(oldForm), ("enrollmentId", d.Enrollment.Id.ToString())));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var verify = fixture.CreateContext();
        Assert.True(await verify.ExamStudents.AnyAsync(x => x.Id == d.Enrollment.Id));
    }

    private static HttpClient Client(WebApplicationFactory<IndexModel> factory) => factory.CreateClient(new()
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task AssertDeniedAsync(HttpClient client, HttpResponseMessage response)
    {
        // MVC cookies redirect forbidden requests to the existing 403 page.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Home/StatusCode/403", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(response.Headers.Location)).StatusCode);
    }

    // Exercise real antiforgery cookies and real account login; no test authentication bypass.
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    private static FormUrlEncodedContent Form(string token, params (string Key, string Value)[] fields)
    {
        var data = fields.ToDictionary(x => x.Key, x => x.Value);
        data["__RequestVerificationToken"] = token;
        return new(data);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var html = await client.GetStringAsync("/Account/Login");
        var response = await client.PostAsync("/Account/Login", Form(Token(html), ("Input.Email", email), ("Input.Password", Password)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private WebApplicationFactory<IndexModel> Factory() => new WebApplicationFactory<IndexModel>().WithWebHostBuilder(web =>
    {
        web.UseEnvironment("Testing");
        web.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString,
            ["Jwt:Key"] = "roster-tests-only-signing-key-32-bytes-minimum",
            ["Jwt:Issuer"] = "Aives.Tests", ["Jwt:Audience"] = "Aives.Tests.Client", ["Jwt:ExpirationMinutes"] = "60"
        }));
        web.ConfigureServices(services =>
        {
            // The fixture owns a disposable database; never use the application's configured database here.
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(fixture.ConnectionString));
        });
    });
}
