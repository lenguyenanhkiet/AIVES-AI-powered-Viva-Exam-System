using System.Net;
using System.Net.Http.Json;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using AIpoweredVivaExamSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

[Collection("SQL Server")]
public class TopicApiTests(SqlServerFixture fixture)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = SubjectApiTests.Json;

    private async Task<Guid> CreateSubjectAsync()
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/subjects",
            new CreateSubjectRequest("T" + Guid.NewGuid().ToString("N")[..12], "Subject", null), Json);
        return (await response.Content.ReadFromJsonAsync<SubjectResponse>(Json))!.Id;
    }

    [Fact]
    public async Task Crud_round_trip_filtered_by_subject()
    {
        var subjectId = await CreateSubjectAsync();
        var otherSubjectId = await CreateSubjectAsync();
        var created = await fixture.Client.PostAsJsonAsync("/api/topics",
            new CreateTopicRequest(subjectId, "LINQ", "Queries"), Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var topic = (await created.Content.ReadFromJsonAsync<TopicResponse>(Json))!;
        Assert.Equal(subjectId, topic.SubjectId);
        Assert.Equal(AcademicStatus.Active, topic.Status);
        await fixture.Client.PostAsJsonAsync("/api/topics", new CreateTopicRequest(otherSubjectId, "LINQ", null), Json);

        var updated = await fixture.Client.PutAsJsonAsync($"/api/topics/{topic.Id}",
            new UpdateTopicRequest("EF Core", null, AcademicStatus.Inactive), Json);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var result = (await updated.Content.ReadFromJsonAsync<TopicResponse>(Json))!;
        Assert.Equal("EF Core", result.Name);
        Assert.Equal(subjectId, result.SubjectId);
        Assert.NotNull(result.UpdatedAt);

        var page = await fixture.Client.GetFromJsonAsync<PagedResponse<TopicResponse>>(
            $"/api/topics?subjectId={subjectId}", Json);
        Assert.Equal(topic.Id, Assert.Single(page!.Items).Id);

        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.DeleteAsync($"/api/topics/{topic.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/api/topics/{topic.Id}")).StatusCode);
        await using var context = fixture.CreateContext();
        Assert.NotNull((await context.Topics.SingleAsync(x => x.Id == topic.Id)).DeletedAt);
        // A soft-deleted topic frees its name within the subject.
        Assert.Equal(HttpStatusCode.Created, (await fixture.Client.PostAsJsonAsync("/api/topics",
            new CreateTopicRequest(subjectId, "EF Core", null), Json)).StatusCode);
    }

    [Fact]
    public async Task Duplicate_name_in_same_subject_returns_409()
    {
        var subjectId = await CreateSubjectAsync();
        Assert.Equal(HttpStatusCode.Created, (await fixture.Client.PostAsJsonAsync("/api/topics",
            new CreateTopicRequest(subjectId, "Delegates", null), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync("/api/topics",
            new CreateTopicRequest(subjectId, " Delegates ", null), Json)).StatusCode);
    }

    [Fact]
    public async Task Missing_or_deleted_subject_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsJsonAsync("/api/topics",
            new CreateTopicRequest(Guid.NewGuid(), "Orphan", null), Json)).StatusCode);
        var subjectId = await CreateSubjectAsync();
        await fixture.Client.DeleteAsync($"/api/subjects/{subjectId}");
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsJsonAsync("/api/topics",
            new CreateTopicRequest(subjectId, "Orphan", null), Json)).StatusCode);
    }

    [Fact]
    public async Task Delete_topic_with_question_returns_409()
    {
        var questionId = await fixture.SeedQuestionAsync();
        await using var context = fixture.CreateContext();
        var topicId = (await context.Questions.SingleAsync(x => x.Id == questionId)).TopicId!.Value;
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.DeleteAsync($"/api/topics/{topicId}")).StatusCode);
    }

    [Theory]
    [InlineData("subjectId=not-a-guid")]
    [InlineData("subjectId=00000000-0000-0000-0000-000000000000")]
    [InlineData("pageSize=0")]
    public async Task Invalid_list_query_returns_400(string query) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync("/api/topics?" + query)).StatusCode);

    [Fact]
    public async Task Invalid_create_returns_400() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/api/topics",
            new { subjectId = Guid.Empty, name = "" })).StatusCode);
}
