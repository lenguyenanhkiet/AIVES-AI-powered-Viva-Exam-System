using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Questions;
using AIpoweredVivaExamSystem.Application.Questions.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using AIpoweredVivaExamSystem.Persistence.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

[Collection("SQL Server")]
public class QuestionServiceTests(SqlServerFixture fixture)
{
    private static QuestionService Service(ApplicationDbContext context) => new(
        new QuestionRepository(context), new CreateQuestionRequestValidator(),
        new UpdateQuestionRequestValidator(), new QuestionListQueryValidator());

    private async Task<(Subject Subject, Topic Topic)> SeedSubjectAsync()
    {
        await using var context = fixture.CreateContext();
        var subject = new Subject("Q_" + Guid.NewGuid().ToString("N")[..20], "Question tests");
        var topic = new Topic(subject.Id, "Dependency injection");
        context.AddRange(subject, topic);
        await context.SaveChangesAsync();
        return (subject, topic);
    }

    private static CreateQuestionRequest NewRequest(Guid subjectId, Guid? topicId) => new(
        subjectId, topicId, "Explain AddScoped.", "Per request.", BloomLevel.Understand, QuestionDifficulty.Easy);

    [Fact]
    public async Task Create_rejects_missing_subject_missing_topic_and_topic_of_another_subject()
    {
        var (subject, topic) = await SeedSubjectAsync();
        var (_, otherTopic) = await SeedSubjectAsync();
        await using var context = fixture.CreateContext();
        var service = Service(context);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => service.CreateAsync(NewRequest(Guid.NewGuid(), null), default));
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => service.CreateAsync(NewRequest(subject.Id, Guid.NewGuid()), default));
        await Assert.ThrowsAsync<DomainValidationException>(() => service.CreateAsync(NewRequest(subject.Id, otherTopic.Id), default));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(NewRequest(subject.Id, topic.Id) with { Content = "" }, default));
        Assert.False(await context.Questions.AnyAsync(x => x.SubjectId == subject.Id));
    }

    [Fact]
    public async Task Approve_requires_a_rubric_and_editing_returns_to_draft()
    {
        var (subject, topic) = await SeedSubjectAsync();
        await using var context = fixture.CreateContext();
        var service = Service(context);
        var created = await service.CreateAsync(NewRequest(subject.Id, topic.Id), default);
        Assert.Equal(QuestionStatus.Draft, created.Status);
        Assert.False(created.HasRubric);

        await Assert.ThrowsAsync<ResourceConflictException>(() => service.ApproveAsync(created.Id, default));

        context.Add(new Rubric(created.Id, "Rubric", null, [new CriterionDefinition(null, "Lifetime", null, null, 5, 1)]));
        await context.SaveChangesAsync();
        var approved = await service.ApproveAsync(created.Id, default);
        Assert.Equal(QuestionStatus.Approved, approved.Status);
        Assert.True(approved.HasRubric);

        var edited = await service.UpdateAsync(created.Id, new UpdateQuestionRequest(
            null, "Compare lifetimes.", null, BloomLevel.Analyze, QuestionDifficulty.Hard), default);
        Assert.Equal(QuestionStatus.Draft, edited.Status);
        Assert.Null(edited.TopicId);
    }

    [Fact]
    public async Task Delete_removes_the_rubric_and_unblocks_topic_deletion()
    {
        var (subject, topic) = await SeedSubjectAsync();
        Guid questionId;
        await using (var context = fixture.CreateContext())
        {
            questionId = (await Service(context).CreateAsync(NewRequest(subject.Id, topic.Id), default)).Id;
            context.Add(new Rubric(questionId, "Rubric", null, [new CriterionDefinition(null, "Lifetime", null, null, 5, 1)]));
            await context.SaveChangesAsync();
            Assert.True(await new TopicRepository(context).HasActiveQuestionsAsync(topic.Id, default));
        }

        await using (var context = fixture.CreateContext())
            await Service(context).DeleteAsync(questionId, default);

        await using var verify = fixture.CreateContext();
        Assert.False(await verify.Rubrics.AnyAsync(x => x.QuestionId == questionId));
        Assert.False(await verify.RubricCriteria.AnyAsync(x => !verify.Rubrics.Any(r => r.Id == x.RubricId)));
        Assert.False(await new TopicRepository(verify).HasActiveQuestionsAsync(topic.Id, default));
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Service(verify).GetAsync(questionId, default));
    }

    [Fact]
    public async Task List_filters_and_reports_rubric_presence()
    {
        var (subject, topic) = await SeedSubjectAsync();
        await using var context = fixture.CreateContext();
        var service = Service(context);
        var withRubric = await service.CreateAsync(NewRequest(subject.Id, topic.Id), default);
        await service.CreateAsync(NewRequest(subject.Id, null) with
        {
            Content = "Design a caching layer.", BloomLevel = BloomLevel.Analyze, Difficulty = QuestionDifficulty.Hard
        }, default);
        context.Add(new Rubric(withRubric.Id, "Rubric", null, [new CriterionDefinition(null, "Lifetime", null, null, 5, 1)]));
        await context.SaveChangesAsync();

        var all = await service.ListAsync(new QuestionListQuery { SubjectId = subject.Id }, default);
        Assert.Equal(2, all.TotalCount);
        Assert.Single(all.Items, x => x.HasRubric && x.Id == withRubric.Id);

        var byTopic = await service.ListAsync(new QuestionListQuery { TopicId = topic.Id }, default);
        Assert.Equal(withRubric.Id, Assert.Single(byTopic.Items).Id);

        var hard = await service.ListAsync(new QuestionListQuery
        {
            SubjectId = subject.Id, BloomLevel = BloomLevel.Analyze, Difficulty = QuestionDifficulty.Hard, Keyword = "caching"
        }, default);
        Assert.Equal("Design a caching layer.", Assert.Single(hard.Items).Content);

        var approved = await service.ListAsync(new QuestionListQuery { SubjectId = subject.Id, Status = QuestionStatus.Approved }, default);
        Assert.Equal(0, approved.TotalCount);
    }
}
