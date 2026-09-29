using System.Net;
using System.Net.Http.Json;
using System.Text;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

[Collection("SQL Server")]
public class RubricApiTests(SqlServerFixture fixture)
{
    private static CreateRubricRequest Request(Guid questionId) => new(questionId, "DI rubric", null,
        [new("Definition", null, "Inversion of control", 4, 1), new("Example", null, null, 6, 2)]);

    [Fact]
    public async Task Crud_persists_criteria_ids_audit_fields_order_and_total()
    {
        var questionId = await fixture.SeedQuestionAsync();
        var created = await fixture.Client.PostAsJsonAsync("/api/rubrics", Request(questionId));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var rubric = (await created.Content.ReadFromJsonAsync<RubricResponse>())!;
        Assert.Equal(10, rubric.TotalScore);
        Assert.NotEqual(default, rubric.CreatedAt);
        Assert.EndsWith($"/api/rubrics/{rubric.Id}", created.Headers.Location!.ToString());
        var get = await fixture.Client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var retained = rubric.Criteria[0];
        var removedId = rubric.Criteria[1].Id;
        var update = new UpdateRubricRequest("Updated", "Description",
            [new("Updated definition", null, null, 5, 2, retained.Id), new("New criterion", null, null, 3, 1)]);
        var updated = await fixture.Client.PutAsJsonAsync($"/api/rubrics/{rubric.Id}", update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var result = (await updated.Content.ReadFromJsonAsync<RubricResponse>())!;
        Assert.Equal(8, result.TotalScore);
        Assert.NotNull(result.UpdatedAt);
        Assert.Equal(retained.Id, result.Criteria[1].Id);
        Assert.DoesNotContain(result.Criteria, x => x.Id == removedId);
        Assert.Equal(new[] { 1, 2 }, result.Criteria.Select(x => x.DisplayOrder));

        // Swap existing positions without replacing their identities.
        var swap = new UpdateRubricRequest(result.Name, result.Description, result.Criteria.Select(x =>
            new CriterionRequest(x.Name, x.Description, x.ExpectedConcepts, x.MaxScore, 3 - x.DisplayOrder, x.Id)).ToList());
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.PutAsJsonAsync($"/api/rubrics/{rubric.Id}", swap)).StatusCode);
        var page = await fixture.Client.GetFromJsonAsync<PagedResponse<RubricResponse>>($"/api/rubrics?questionId={questionId}&page=1&pageSize=1");
        Assert.Equal(1, page!.TotalCount);
        Assert.Equal(rubric.Id, Assert.Single(page.Items).Id);
        var emptyPage = await fixture.Client.GetFromJsonAsync<PagedResponse<RubricResponse>>($"/api/rubrics?questionId={questionId}&page=2&pageSize=1");
        Assert.Empty(emptyPage!.Items);

        await using (var context = fixture.CreateContext())
        {
            Assert.False(await context.RubricCriteria.AnyAsync(x => x.Id == removedId));
            var saved = await context.Rubrics.Include(x => x.Criteria).SingleAsync(x => x.Id == rubric.Id);
            Assert.Equal(saved.TotalScore, saved.Criteria.Sum(x => x.MaxScore));
            Assert.All(saved.Criteria, x => Assert.NotEqual(default, x.CreatedAt));
        }
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.DeleteAsync($"/api/rubrics/{rubric.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/api/rubrics/{rubric.Id}")).StatusCode);
        await using var afterDelete = fixture.CreateContext();
        Assert.False(await afterDelete.RubricCriteria.AnyAsync(x => x.RubricId == rubric.Id));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[null]")]
    [InlineData("[{\"name\":\"Score\",\"maxScore\":0,\"displayOrder\":1}]")]
    [InlineData("[{\"name\":\"Score\",\"maxScore\":0.001,\"displayOrder\":1}]")]
    [InlineData("[{\"name\":\"Score\",\"maxScore\":1,\"displayOrder\":0}]")]
    [InlineData("[{\"name\":\" \",\"maxScore\":1,\"displayOrder\":1}]")]
    public async Task Invalid_criteria_return_problem_details(string criteria)
    {
        var body = $$"""{"questionId":"{{Guid.NewGuid()}}","name":"Rubric","criteria":{{criteria}}} """;
        var response = await fixture.Client.PostAsync("/api/rubrics", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647")]
    [InlineData("questionId=not-a-guid")]
    public async Task Invalid_pagination_or_filter_returns_400(string query) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync("/api/rubrics?" + query)).StatusCode);

    [Fact]
    public async Task Missing_question_and_missing_rubric_return_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsJsonAsync("/api/rubrics", Request(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.DeleteAsync($"/api/rubrics/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PutAsJsonAsync($"/api/rubrics/{Guid.NewGuid()}",
            new UpdateRubricRequest("Rubric", null, Request(Guid.NewGuid()).Criteria))).StatusCode);
    }

    [Fact]
    public async Task Concurrent_creates_allow_exactly_one_rubric()
    {
        var questionId = await fixture.SeedQuestionAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => fixture.Client.PostAsJsonAsync("/api/rubrics", Request(questionId))));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Equal(3, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.Rubrics.CountAsync(x => x.QuestionId == questionId));
    }

    [Fact]
    public async Task Invalid_domain_update_leaves_saved_data_unchanged()
    {
        var questionId = await fixture.SeedQuestionAsync();
        var response = await fixture.Client.PostAsJsonAsync("/api/rubrics", Request(questionId));
        var rubric = (await response.Content.ReadFromJsonAsync<RubricResponse>())!;
        var invalid = new UpdateRubricRequest("Changed", null,
            [new("Unknown", null, null, 2, 1, Guid.NewGuid())]);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PutAsJsonAsync($"/api/rubrics/{rubric.Id}", invalid)).StatusCode);
        var saved = await fixture.Client.GetFromJsonAsync<RubricResponse>($"/api/rubrics/{rubric.Id}");
        Assert.Equal(rubric.Name, saved!.Name);
        Assert.Equal(rubric.TotalScore, saved.TotalScore);
        Assert.Equal(rubric.Criteria.Select(x => x.Id), saved.Criteria.Select(x => x.Id));
    }
}
