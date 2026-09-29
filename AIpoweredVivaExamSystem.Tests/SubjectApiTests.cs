using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

[Collection("SQL Server")]
public class SubjectApiTests(SqlServerFixture fixture)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static string UniqueCode() => "S" + Guid.NewGuid().ToString("N")[..12];

    [Fact]
    public async Task Crud_round_trip_with_soft_delete()
    {
        var code = UniqueCode();
        var created = await fixture.Client.PostAsJsonAsync("/api/subjects",
            new CreateSubjectRequest(code, "Programming with C#", "Intro", AcademicStatus.Active), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var subject = (await created.Content.ReadFromJsonAsync<SubjectResponse>(Json))!;
        Assert.Equal(code, subject.Code);
        Assert.NotEqual(default, subject.CreatedAt);
        Assert.EndsWith($"/api/subjects/{subject.Id}", created.Headers.Location!.ToString());
        Assert.Contains("\"status\":\"Active\"", await created.Content.ReadAsStringAsync());

        var newCode = UniqueCode();
        var updated = await fixture.Client.PutAsJsonAsync($"/api/subjects/{subject.Id}",
            new UpdateSubjectRequest(newCode, "Updated", null, AcademicStatus.Inactive), Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var result = (await updated.Content.ReadFromJsonAsync<SubjectResponse>(Json))!;
        Assert.Equal(newCode, result.Code);
        Assert.Equal(AcademicStatus.Inactive, result.Status);
        Assert.NotNull(result.UpdatedAt);

        var page = await fixture.Client.GetFromJsonAsync<PagedResponse<SubjectResponse>>(
            $"/api/subjects?keyword={newCode}&status=Inactive", Json);
        Assert.Equal(subject.Id, Assert.Single(page!.Items).Id);

        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.DeleteAsync($"/api/subjects/{subject.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/api/subjects/{subject.Id}")).StatusCode);
        var afterDelete = await fixture.Client.GetFromJsonAsync<PagedResponse<SubjectResponse>>(
            $"/api/subjects?keyword={newCode}", Json);
        Assert.Empty(afterDelete!.Items);
        await using var context = fixture.CreateContext();
        Assert.NotNull((await context.Subjects.SingleAsync(x => x.Id == subject.Id)).DeletedAt);
        // A soft-deleted subject releases its code.
        Assert.Equal(HttpStatusCode.Created, (await fixture.Client.PostAsJsonAsync("/api/subjects",
            new CreateSubjectRequest(newCode, "Recreated", null), Json)).StatusCode);
    }

    [Fact]
    public async Task Duplicate_code_returns_409()
    {
        var code = UniqueCode();
        var first = await fixture.Client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest(code, "A", null), Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var second = await fixture.Client.PostAsJsonAsync("/api/subjects", new CreateSubjectRequest(code, "B", null), Json);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var other = (await (await fixture.Client.PostAsJsonAsync("/api/subjects",
            new CreateSubjectRequest(UniqueCode(), "C", null), Json)).Content.ReadFromJsonAsync<SubjectResponse>(Json))!;
        var rename = await fixture.Client.PutAsJsonAsync($"/api/subjects/{other.Id}",
            new UpdateSubjectRequest(code, "C", null, AcademicStatus.Active), Json);
        Assert.Equal(HttpStatusCode.Conflict, rename.StatusCode);
    }

    [Fact]
    public async Task Delete_with_active_topic_returns_409()
    {
        var subject = (await (await fixture.Client.PostAsJsonAsync("/api/subjects",
            new CreateSubjectRequest(UniqueCode(), "Has topics", null), Json)).Content.ReadFromJsonAsync<SubjectResponse>(Json))!;
        Assert.Equal(HttpStatusCode.Created, (await fixture.Client.PostAsJsonAsync("/api/topics",
            new { subjectId = subject.Id, name = "LINQ" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.DeleteAsync($"/api/subjects/{subject.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("""{"code":"","name":"Name"}""")]
    [InlineData("""{"code":"CODE","name":" "}""")]
    [InlineData("""{"code":"CODE","name":"Name","status":"Unknown"}""")]
    [InlineData("""{"code":"CODE","name":"Name","status":99}""")]
    public async Task Invalid_create_returns_400(string body)
    {
        var response = await fixture.Client.PostAsync("/api/subjects",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("status=Unknown")]
    public async Task Invalid_list_query_returns_400(string query) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync("/api/subjects?" + query)).StatusCode);

    [Fact]
    public async Task Missing_subject_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/api/subjects/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.DeleteAsync($"/api/subjects/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PutAsJsonAsync($"/api/subjects/{Guid.NewGuid()}",
            new UpdateSubjectRequest("CODE", "Name", null, AcademicStatus.Active), Json)).StatusCode);
    }
}
