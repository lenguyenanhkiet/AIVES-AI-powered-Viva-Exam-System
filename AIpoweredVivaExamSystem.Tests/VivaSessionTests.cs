using System.Text.Json;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using AIpoweredVivaExamSystem.Persistence.Repositories;
using AIpoweredVivaExamSystem.Web.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class SessionSelectionTests
{
    [Fact]
    public void SelectionAvoidsPreviousQuestionsAndNeverDuplicates()
    {
        var ids = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        for (var i = 0; i < 100; i++)
        {
            var selected = VivaSessionStore.Select(ids, ids[..2], 2);
            Assert.Equal(2, selected.Distinct().Count());
            Assert.DoesNotContain(selected, id => ids[..2].Contains(id));
            Assert.All(selected, id => Assert.Contains(id, ids));
        }
        Assert.Equal(6, VivaSessionStore.Select(ids, ids[..4], 6).Distinct().Count());
        Assert.Throws<DomainValidationException>(() => VivaSessionStore.Select(ids, [], 7));
    }

    [Fact]
    public void CompletionIsIdempotentAndStudentDtoDoesNotExposeGradingData()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new VivaSession(Guid.NewGuid(), now);
        Assert.Throws<DomainValidationException>(() => session.Complete(now.AddSeconds(-1)));
        session.Complete(now.AddSeconds(1));
        session.Complete(now.AddMinutes(1));
        Assert.Equal(now.AddSeconds(1), session.CompletedAt);
        var json = JsonSerializer.Serialize(new StudentSession(session.Id, now, null, [new(Guid.NewGuid(), 1, "Question")]));
        Assert.DoesNotContain("Rubric", json);
        Assert.DoesNotContain("ExpectedAnswer", json);
    }
}

[Collection("SQL Server")]
public sealed class VivaSessionTests(SqlServerFixture fixture)
{
    private sealed record Actor(Guid Id, bool IsAdministrator = false, bool IsLecturer = false) : ICurrentUser;
    private sealed class Notifier : IExamNotifier
    {
        public int Calls;
        public bool Fail;
        public Task NotifyChangedAsync(Guid id, CancellationToken ct) { Calls++; if (Fail) throw new IOException("Offline"); return Task.CompletedTask; }
    }
    private ApplicationDbContext Context(Actor actor) => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer(fixture.ConnectionString).Options, actor);
    private IVivaSessionService Service(ApplicationDbContext db, Actor actor, Notifier notifier) => new VivaSessionService(
        new VivaSessionStore(db, new ExamUnitOfWork(db, notifier, NullLogger<ExamUnitOfWork>.Instance), actor),
        new ExamAccessGuard(new ExamFoundationStore(db), actor), actor);

    private async Task<ExamDemo> SeedAsync(int poolSize = 4, int slotOffset = -1, ExamStatus status = ExamStatus.Ongoing)
    {
        await using var db = fixture.CreateContext();
        var demo = await ExamDemo.SeedAsync(db);
        db.Entry(demo.Exam).Property(e => e.StartAt).CurrentValue = DateTimeOffset.UtcNow.AddHours(-1);
        db.Entry(demo.Exam).Property(e => e.EndAt).CurrentValue = DateTimeOffset.UtcNow.AddHours(2);
        db.Entry(demo.Exam).Property(e => e.Status).CurrentValue = status;
        demo.Enrollment.Schedule(DateTimeOffset.UtcNow.AddMinutes(slotOffset));
        for (var i = 0; i < poolSize; i++)
        {
            var q = new Question(demo.Subject.Id, "Question " + i, BloomLevel.Understand, QuestionDifficulty.Medium,
                "SECRET ANSWER") { CreatedBy = demo.Lecturer.Id };
            q.Approve();
            db.AddRange(q, new ExamQuestionPool(demo.Exam.Id, q.Id),
                new Rubric(q.Id, "SECRET RUBRIC", null, [new CriterionDefinition(null, "Criterion", null, "SECRET CONCEPT", 10, 1)]));
        }
        await db.SaveChangesAsync();
        return demo;
    }

    [Fact]
    public async Task ConcurrentOpenRefreshSnapshotAndCompletionRemainStable()
    {
        var d = await SeedAsync();
        var actor = new Actor(d.Student.Id);
        async Task<StudentSession> Open()
        {
            await using var db = Context(actor);
            return await Service(db, actor, new Notifier()).GetOrCreateMySessionAsync(d.Enrollment.Id, default);
        }
        var results = await Task.WhenAll(Open(), Open());
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(results[0].Questions.ToArray(), results[1].Questions.ToArray());
        Assert.Equal(2, results[0].Questions.Select(q => q.Id).Distinct().Count());
        await using (var db = fixture.CreateContext())
        {
            var q = await db.Questions.SingleAsync(q => q.Id == results[0].Questions[0].Id);
            q.Update("Changed content", "Changed answer", q.BloomLevel, q.Difficulty, null);
            db.Entry(d.Exam).State = EntityState.Unchanged;
            db.Entry(d.Exam).Property(e => e.Status).CurrentValue = ExamStatus.Completed;
            await db.SaveChangesAsync();
            Assert.Contains("SECRET RUBRIC", (await db.SessionQuestions.FirstAsync(q => q.VivaSessionId == results[0].Id)).RubricSnapshot);
        }
        var reopened = await Open();
        Assert.Equal(results[0].Questions.ToArray(), reopened.Questions.ToArray());
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(reopened));
        await using (var db = Context(actor))
        {
            var notifier = new Notifier { Fail = true };
            var service = Service(db, actor, notifier);
            await service.CompleteMySessionAsync(reopened.Id, default);
            await service.CompleteMySessionAsync(reopened.Id, default);
            Assert.Equal(1, notifier.Calls);
        }
        Assert.NotNull((await Open()).CompletedAt);
    }

    [Theory]
    [InlineData(1, -1, ExamStatus.Ongoing)]
    [InlineData(4, 20, ExamStatus.Ongoing)]
    [InlineData(4, -20, ExamStatus.Ongoing)]
    [InlineData(4, -1, ExamStatus.Completed)]
    [InlineData(4, -1, ExamStatus.Cancelled)]
    public async Task InvalidPoolSlotOrLifecycleDoesNotPersistOrNotify(int pool, int offset, ExamStatus status)
    {
        var d = await SeedAsync(pool, offset);
        var actor = new Actor(d.Student.Id);
        await using var db = Context(actor);
        // Reused contexts must observe lifecycle changes made after the exam was tracked.
        await db.Exams.IgnoreQueryFilters().SingleAsync(e => e.Id == d.Exam.Id);
        await using (var update = fixture.CreateContext())
            await update.Exams.Where(e => e.Id == d.Exam.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(e => e.Status, status));
        var notifier = new Notifier();
        await Assert.ThrowsAsync<DomainValidationException>(() => Service(db, actor, notifier).GetOrCreateMySessionAsync(d.Enrollment.Id, default));
        Assert.False(await db.VivaSessions.AnyAsync(s => s.ExamStudentId == d.Enrollment.Id));
        Assert.Equal(0, notifier.Calls);
    }

    [Fact]
    public async Task OtherStudentCannotOpenSessionAndLiveAccessRevokesImmediately()
    {
        var d = await SeedAsync();
        var actor = new Actor(Guid.NewGuid());
        await using var db = Context(actor);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(db, actor, new Notifier()).GetOrCreateMySessionAsync(d.Enrollment.Id, default));
        var access = new ExamRealtimeAccess(new LoginUserRepository(db), new ExamFoundationStore(db));
        Assert.True(await access.CanManageAsync(d.Lecturer.Id, d.Exam.Id, default));
        Assert.False(await access.CanManageAsync(d.Student.Id, d.Exam.Id, default));
        db.LecturerSubjects.Remove(await db.LecturerSubjects.SingleAsync(a => a.LecturerId == d.Lecturer.Id));
        await db.SaveChangesAsync();
        Assert.False(await access.CanManageAsync(d.Lecturer.Id, d.Exam.Id, default));
    }

    [Fact]
    public async Task NextStudentAvoidsPreviousSetAndRevalidatesPool()
    {
        var d = await SeedAsync();
        var firstActor = new Actor(d.Student.Id);
        StudentSession first;
        await using (var db = Context(firstActor))
            first = await Service(db, firstActor, new Notifier()).GetOrCreateMySessionAsync(d.Enrollment.Id, default);
        var second = new User(Guid.NewGuid() + "@example.test", "unused", "Second", UserStatus.Active);
        var entry = new ExamStudent(d.Exam.Id, second.Id);
        entry.Schedule(DateTimeOffset.UtcNow.AddMinutes(-1));
        await using (var db = fixture.CreateContext()) { db.AddRange(second, entry); await db.SaveChangesAsync(); }
        var actor = new Actor(second.Id);
        await using (var db = Context(actor))
        {
            var next = await Service(db, actor, new Notifier()).GetOrCreateMySessionAsync(entry.Id, default);
            Assert.DoesNotContain(next.Questions, q => first.Questions.Any(old => old.Id == q.Id));
        }
        // Newly drafted pool questions cannot be used for a new enrollment.
        var third = new User(Guid.NewGuid() + "@example.test", "unused", "Third", UserStatus.Active);
        var thirdEntry = new ExamStudent(d.Exam.Id, third.Id);
        thirdEntry.Schedule(DateTimeOffset.UtcNow.AddMinutes(-1));
        await using (var db = fixture.CreateContext())
        {
            foreach (var q in await db.Questions.Where(q => q.SubjectId == d.Subject.Id).ToArrayAsync())
                q.Update(q.Content, q.ExpectedAnswer, q.BloomLevel, q.Difficulty, null);
            db.AddRange(third, thirdEntry); await db.SaveChangesAsync();
        }
        var thirdActor = new Actor(third.Id);
        await using var thirdDb = Context(thirdActor);
        await Assert.ThrowsAsync<DomainValidationException>(() => Service(thirdDb, thirdActor, new Notifier()).GetOrCreateMySessionAsync(thirdEntry.Id, default));
    }

    [Fact]
    public async Task StudentListIsScopedAndManagerDashboardReadsPersistedRoster()
    {
        var d = await SeedAsync();
        await using (var setup = fixture.CreateContext())
        {
            var draft = new Exam(d.Subject.Id, d.Lecturer.Id, "Draft", d.Exam.StartAt, d.Exam.EndAt, 15, 2, 1);
            var cancelled = new Exam(d.Subject.Id, d.Lecturer.Id, "Cancelled", d.Exam.StartAt, d.Exam.EndAt, 15, 2, 1);
            var deleted = new Exam(d.Subject.Id, d.Lecturer.Id, "Deleted", d.Exam.StartAt, d.Exam.EndAt, 15, 2, 1)
                { DeletedAt = DateTimeOffset.UtcNow };
            setup.AddRange(draft, cancelled, deleted, new ExamStudent(draft.Id, d.Student.Id),
                new ExamStudent(cancelled.Id, d.Student.Id), new ExamStudent(deleted.Id, d.Student.Id));
            setup.Entry(cancelled).Property(e => e.Status).CurrentValue = ExamStatus.Cancelled;
            setup.Entry(deleted).Property(e => e.Status).CurrentValue = ExamStatus.Published;
            await setup.SaveChangesAsync();
        }
        var actor = new Actor(d.Student.Id);
        await using (var db = Context(actor))
        {
            var list = await new MyVivaSessionsReader(db, actor).ListAsync(default);
            Assert.Equal(d.Enrollment.Id, Assert.Single(list).EnrollmentId);
            await Service(db, actor, new Notifier()).GetOrCreateMySessionAsync(d.Enrollment.Id, default);
        }
        await using (var other = Context(new Actor(Guid.NewGuid())))
            Assert.Empty(await new MyVivaSessionsReader(other, new Actor(Guid.NewGuid())).ListAsync(default));
        await using var manager = Context(new Actor(d.Lecturer.Id, IsLecturer: true));
        var reader = new ExamLiveReader(manager, new ExamFoundationStore(manager));
        Assert.Equal(d.Exam.Name, (await reader.SummaryAsync(d.Exam.Id, default)).Name);
        var live = await reader.ReadAsync(d.Exam.Id, default);
        Assert.Equal(ExamStatus.Ongoing, live.Status);
        Assert.Equal(1, live.Counts.Sessions);
        Assert.Equal(SessionState.InProgress, Assert.Single(live.Roster).SessionStatus);
    }
}
