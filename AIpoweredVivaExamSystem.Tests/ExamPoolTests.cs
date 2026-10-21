using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using AIpoweredVivaExamSystem.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

[Collection("SQL Server")]
public sealed class ExamPoolTests(SqlServerFixture fixture)
{
    private sealed record Actor(Guid Id, bool IsAdministrator = false, bool IsLecturer = false) : ICurrentUser;

    private sealed class Notifier(Func<Guid, Task>? afterCommit = null) : IExamNotifier
    {
        public List<Guid> Events { get; } = [];
        public async Task NotifyChangedAsync(Guid examId, CancellationToken ct)
        {
            Events.Add(examId);
            if (afterCommit is not null) await afterCommit(examId);
        }
    }

    private ApplicationDbContext Context(Actor actor) => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer(fixture.ConnectionString).Options, actor);

    private ExamPoolService Service(ApplicationDbContext db, Actor actor, Notifier notifier) => new(
        new ExamPoolStore(db), new ExamAccessGuard(new ExamFoundationStore(db), actor),
        new ExamUnitOfWork(db, notifier, NullLogger<ExamUnitOfWork>.Instance));

    private async Task<(ExamDemo Demo, Question Eligible)> SeedWithEligibleQuestionAsync()
    {
        await using var db = fixture.CreateContext();
        var demo = await ExamDemo.SeedAsync(db);
        var eligible = NewQuestion(demo.Subject.Id, demo.Lecturer.Id, "Eligible question", QuestionStatus.Approved);
        db.AddRange(eligible, NewRubric(eligible.Id));
        await db.SaveChangesAsync();
        return (demo, eligible);
    }

    private static Question NewQuestion(Guid subjectId, Guid ownerId, string content, QuestionStatus status)
    {
        var question = new Question(subjectId, content, BloomLevel.Understand, QuestionDifficulty.Medium)
            { CreatedBy = ownerId };
        if (status == QuestionStatus.Approved) question.Approve();
        else if (status == QuestionStatus.Rejected) question.Reject();
        return question;
    }

    private static Rubric NewRubric(Guid questionId) => new(questionId, "Pool rubric", null,
        [new CriterionDefinition(null, "Reasoning", null, null, 10, 1)]);

    [Fact]
    public async Task PickerShowsOnlyQuestionsThatPassEveryEligibilityRuleAndInvalidBatchAddsNothing()
    {
        var (demo, eligible) = await SeedWithEligibleQuestionAsync();
        var draft = NewQuestion(demo.Subject.Id, demo.Lecturer.Id, "Draft", QuestionStatus.Draft);
        var rejected = NewQuestion(demo.Subject.Id, demo.Lecturer.Id, "Rejected", QuestionStatus.Rejected);
        var noRubric = NewQuestion(demo.Subject.Id, demo.Lecturer.Id, "Missing rubric", QuestionStatus.Approved);
        var wrongOwner = NewQuestion(demo.Subject.Id, demo.Student.Id, "Other owner", QuestionStatus.Approved);
        var otherSubject = new Subject("OTHER" + Guid.NewGuid().ToString("N"), "Other subject");
        var wrongSubject = NewQuestion(otherSubject.Id, demo.Lecturer.Id, "Wrong subject", QuestionStatus.Approved);
        await using (var setup = fixture.CreateContext())
        {
            setup.AddRange(draft, NewRubric(draft.Id), rejected, NewRubric(rejected.Id), noRubric, wrongOwner,
                NewRubric(wrongOwner.Id), otherSubject, wrongSubject, NewRubric(wrongSubject.Id));
            await setup.SaveChangesAsync();
        }

        var actor = new Actor(demo.Lecturer.Id, IsLecturer: true);
        var notifier = new Notifier();
        await using (var db = Context(actor))
        {
            var service = Service(db, actor, notifier);
            var page = await service.GetPageAsync(demo.Exam.Id, default);
            Assert.Equal(new[] { eligible.Id }, page.AvailableQuestions.Select(q => q.Id));
            await Assert.ThrowsAsync<DomainValidationException>(() => service.AddAsync(demo.Exam.Id,
                [eligible.Id, draft.Id, rejected.Id, noRubric.Id, wrongOwner.Id, wrongSubject.Id], default));
        }

        await using var verify = fixture.CreateContext();
        Assert.Empty(await verify.ExamQuestionPools.Where(p => p.ExamId == demo.Exam.Id).ToArrayAsync());
        Assert.Empty(notifier.Events);
    }

    [Fact]
    public async Task PooledQuestionEligibilityMatchesTheRulesUsedByThePoolCount()
    {
        var (demo, eligible) = await SeedWithEligibleQuestionAsync();
        await using (var setup = fixture.CreateContext())
        {
            setup.Add(new ExamQuestionPool(demo.Exam.Id, eligible.Id));
            await setup.SaveChangesAsync();
            await setup.Questions.Where(q => q.Id == eligible.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(q => q.CreatedBy, demo.Student.Id));
        }

        var actor = new Actor(demo.Lecturer.Id, IsLecturer: true);
        await using var db = Context(actor);
        var page = await Service(db, actor, new Notifier()).GetPageAsync(demo.Exam.Id, default);

        var pooled = Assert.Single(page.PooledQuestions);
        Assert.True(pooled.HasRubric);
        Assert.Equal(QuestionStatus.Approved, pooled.Status);
        Assert.False(pooled.IsEligible);
        Assert.Equal(0, page.EligiblePoolCount);
    }

    [Fact]
    public async Task InvalidEmptyIdRejectsTheWholeAddBatch()
    {
        var (demo, eligible) = await SeedWithEligibleQuestionAsync();
        var actor = new Actor(demo.Lecturer.Id, IsLecturer: true);
        var notifier = new Notifier();
        await using (var db = Context(actor))
        {
            var service = Service(db, actor, notifier);
            await Assert.ThrowsAsync<DomainValidationException>(() =>
                service.AddAsync(demo.Exam.Id, [eligible.Id, Guid.Empty], default));
        }

        await using var verify = fixture.CreateContext();
        Assert.Empty(await verify.ExamQuestionPools.Where(p => p.ExamId == demo.Exam.Id).ToArrayAsync());
        Assert.Empty(notifier.Events);
    }

    [Fact]
    public async Task DuplicateSelectionIsIdempotentAndNotifiesOnceAfterCommit()
    {
        var (demo, eligible) = await SeedWithEligibleQuestionAsync();
        var observedCommittedData = false;
        var notifier = new Notifier(async examId =>
        {
            await using var verify = fixture.CreateContext();
            observedCommittedData = await verify.ExamQuestionPools.AnyAsync(p => p.ExamId == examId && p.QuestionId == eligible.Id);
            throw new IOException("Simulated notifier outage");
        });
        var actor = new Actor(demo.Lecturer.Id, IsLecturer: true);
        await using (var db = Context(actor))
        {
            var service = Service(db, actor, notifier);
            await service.AddAsync(demo.Exam.Id, [eligible.Id, eligible.Id], default);
            await service.AddAsync(demo.Exam.Id, [eligible.Id], default);
        }

        await using var final = fixture.CreateContext();
        Assert.Single(await final.ExamQuestionPools.Where(p => p.ExamId == demo.Exam.Id && p.QuestionId == eligible.Id).ToArrayAsync());
        Assert.Single(notifier.Events);
        Assert.True(observedCommittedData);
    }

    [Fact]
    public async Task OnlyExamOwnerOrAdministratorCanManagePool()
    {
        var (demo, eligible) = await SeedWithEligibleQuestionAsync();
        var stranger = new Actor(Guid.NewGuid(), IsLecturer: true);
        await using var db = Context(stranger);
        var service = Service(db, stranger, new Notifier());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetPageAsync(demo.Exam.Id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AddAsync(demo.Exam.Id, [eligible.Id], default));
    }

    [Fact]
    public async Task AdministratorCanManageTheAssignedLecturersPool()
    {
        var (demo, eligible) = await SeedWithEligibleQuestionAsync();
        var administrator = new Actor(Guid.NewGuid(), IsAdministrator: true);
        var notifier = new Notifier();
        await using (var db = Context(administrator))
        {
            var service = Service(db, administrator, notifier);
            var page = await service.GetPageAsync(demo.Exam.Id, default);
            Assert.Contains(page.AvailableQuestions, question => question.Id == eligible.Id);
            await service.AddAsync(demo.Exam.Id, [eligible.Id], default);
        }

        await using var verify = fixture.CreateContext();
        Assert.True(await verify.ExamQuestionPools.AnyAsync(pool =>
            pool.ExamId == demo.Exam.Id && pool.QuestionId == eligible.Id));
        Assert.Single(notifier.Events);
    }

    [Fact]
    public async Task RemovingPoolEntriesNotifiesOnlyWhenThePoolChanges()
    {
        var (demo, eligible) = await SeedWithEligibleQuestionAsync();
        await using (var setup = fixture.CreateContext())
        {
            setup.Add(new ExamQuestionPool(demo.Exam.Id, eligible.Id));
            await setup.SaveChangesAsync();
        }

        var actor = new Actor(demo.Lecturer.Id, IsLecturer: true);
        var notifier = new Notifier();
        await using (var db = Context(actor))
        {
            var service = Service(db, actor, notifier);
            await service.RemoveAsync(demo.Exam.Id, [eligible.Id], default);
            await service.RemoveAsync(demo.Exam.Id, [eligible.Id], default);
        }

        await using var verify = fixture.CreateContext();
        Assert.Empty(await verify.ExamQuestionPools.Where(pool => pool.ExamId == demo.Exam.Id).ToArrayAsync());
        Assert.Equal(new[] { demo.Exam.Id }, notifier.Events);
    }

    [Fact]
    public async Task PoolCanOnlyChangeWhileExamIsDraft()
    {
        var (demo, eligible) = await SeedWithEligibleQuestionAsync();
        await using (var setup = fixture.CreateContext())
        {
            setup.Add(new ExamQuestionPool(demo.Exam.Id, eligible.Id));
            await setup.SaveChangesAsync();
            await setup.Exams.Where(e => e.Id == demo.Exam.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(e => e.Status, ExamStatus.Published));
        }

        var actor = new Actor(demo.Lecturer.Id, IsLecturer: true);
        await using var db = Context(actor);
        var service = Service(db, actor, new Notifier());

        await Assert.ThrowsAsync<ResourceConflictException>(() => service.RemoveAsync(demo.Exam.Id, [eligible.Id], default));
    }
}
