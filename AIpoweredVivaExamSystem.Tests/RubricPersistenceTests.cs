using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

[Collection("SQL Server")]
public class RubricPersistenceTests(SqlServerFixture fixture)
{
    private static Rubric NewRubric(Guid questionId) => new(questionId, "Rubric", null,
        [new CriterionDefinition(null, "Definition", null, null, 5, 1)]);

    [Fact]
    public async Task Failed_batch_rolls_back_both_rubrics_and_criteria()
    {
        var questionId = await fixture.SeedQuestionAsync();
        await using (var context = fixture.CreateContext())
        {
            context.AddRange(NewRubric(questionId), NewRubric(questionId));
            var repository = new RubricRepository(context);
            await Assert.ThrowsAsync<ResourceConflictException>(() => repository.SaveChangesAsync(default));
        }
        await using var verify = fixture.CreateContext();
        Assert.False(await verify.Rubrics.AnyAsync(x => x.QuestionId == questionId));
        Assert.False(await verify.RubricCriteria.AnyAsync(x => !verify.Rubrics.Any(r => r.Id == x.RubricId)));
    }

    [Fact]
    public async Task Database_rejects_nonexistent_question()
    {
        await using var context = fixture.CreateContext();
        context.Add(NewRubric(Guid.NewGuid()));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(error.InnerException).Number);
    }

    [Fact]
    public async Task Database_rejects_topic_from_another_subject()
    {
        var first = await fixture.SeedQuestionAsync();
        var second = await fixture.SeedQuestionAsync();
        await using var context = fixture.CreateContext();
        var question = await context.Questions.SingleAsync(x => x.Id == first);
        var wrongTopic = await context.Questions.Where(x => x.Id == second).Select(x => x.TopicId).SingleAsync();
        context.Entry(question).Property(x => x.TopicId).CurrentValue = wrongTopic;
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(error.InnerException).Number);
    }

    [Fact]
    public async Task Concurrent_aggregate_updates_return_conflict_and_preserve_consistent_total()
    {
        var questionId = await fixture.SeedQuestionAsync();
        var rubric = NewRubric(questionId);
        await using (var seed = fixture.CreateContext())
        {
            seed.Add(rubric);
            await seed.SaveChangesAsync();
        }
        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var firstRepo = new RubricRepository(firstContext);
        var secondRepo = new RubricRepository(secondContext);
        var first = (await firstRepo.GetAsync(rubric.Id, default))!;
        var second = (await secondRepo.GetAsync(rubric.Id, default))!;
        first.Update("First", null, [new(first.Criteria.Single().Id, "Definition", null, null, 6, 1)]);
        second.Update("Second", null, [new(second.Criteria.Single().Id, "Definition", null, null, 7, 1)]);
        firstRepo.MarkUpdated(first);
        secondRepo.MarkUpdated(second);
        await firstRepo.SaveChangesAsync(default);
        await Assert.ThrowsAsync<ResourceConflictException>(() => secondRepo.SaveChangesAsync(default));
        await using var verify = fixture.CreateContext();
        var saved = await verify.Rubrics.Include(x => x.Criteria).SingleAsync(x => x.Id == rubric.Id);
        Assert.Equal("First", saved.Name);
        Assert.Equal(6, saved.TotalScore);
        Assert.Equal(saved.TotalScore, saved.Criteria.Sum(x => x.MaxScore));
    }
}
