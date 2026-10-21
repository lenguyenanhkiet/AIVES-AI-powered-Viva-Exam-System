using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.Scheduling;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using AIpoweredVivaExamSystem.Persistence.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public class ExamLifecycleTests
{
    private static readonly Guid SubjectId = Guid.NewGuid();
    private static readonly Guid LecturerId = Guid.NewGuid();

    private static Exam CreateValidDraftExam(DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        var s = start ?? DateTimeOffset.UtcNow.AddHours(1);
        var e = end ?? s.AddHours(2);
        return new Exam(SubjectId, LecturerId, " PRN222 Final Exam ", s, e, 15, 3, 2);
    }

    [Fact]
    public void Constructor_initializes_valid_draft_exam_and_trims_name()
    {
        var exam = CreateValidDraftExam();

        Assert.NotEqual(Guid.Empty, exam.Id);
        Assert.Equal(SubjectId, exam.SubjectId);
        Assert.Equal(LecturerId, exam.LecturerId);
        Assert.Equal("PRN222 Final Exam", exam.Name);
        Assert.Equal(15, exam.DurationMinutes);
        Assert.Equal(3, exam.NumberOfMainQuestions);
        Assert.Equal(2, exam.MaxFollowUpPerQuestion);
        Assert.Equal(ExamStatus.Draft, exam.Status);
    }

    [Theory]
    [InlineData("", 15, 3, 2)]
    [InlineData("   ", 15, 3, 2)]
    [InlineData("Valid", 0, 3, 2)]
    [InlineData("Valid", -5, 3, 2)]
    [InlineData("Valid", 15, 0, 2)]
    [InlineData("Valid", 15, -1, 2)]
    [InlineData("Valid", 15, 3, -1)]
    public void Constructor_rejects_invalid_inputs(string name, int duration, int mainQuestions, int followups)
    {
        var start = DateTimeOffset.UtcNow.AddHours(1);
        var end = start.AddHours(2);

        Assert.Throws<DomainValidationException>(() =>
            new Exam(SubjectId, LecturerId, name, start, end, duration, mainQuestions, followups));
    }

    [Fact]
    public void Constructor_rejects_invalid_time_windows()
    {
        var start = DateTimeOffset.UtcNow.AddHours(2);
        var end = DateTimeOffset.UtcNow.AddHours(1);

        // start >= end
        Assert.Throws<DomainValidationException>(() =>
            new Exam(SubjectId, LecturerId, "Name", start, end, 15, 3, 2));

        // window < duration
        var shortEnd = start.AddMinutes(10);
        Assert.Throws<DomainValidationException>(() =>
            new Exam(SubjectId, LecturerId, "Name", start, shortEnd, 30, 3, 2));
    }

    [Fact]
    public void Update_modifies_fields_when_draft()
    {
        var exam = CreateValidDraftExam();
        var newStart = DateTimeOffset.UtcNow.AddDays(1);
        var newEnd = newStart.AddHours(3);

        exam.Update("  Updated Name  ", newStart, newEnd, 20, 4, 1);

        Assert.Equal("Updated Name", exam.Name);
        Assert.Equal(newStart, exam.StartAt);
        Assert.Equal(newEnd, exam.EndAt);
        Assert.Equal(20, exam.DurationMinutes);
        Assert.Equal(4, exam.NumberOfMainQuestions);
        Assert.Equal(1, exam.MaxFollowUpPerQuestion);
    }

    [Fact]
    public void Update_throws_when_exam_is_not_draft()
    {
        var exam = CreateValidDraftExam();
        exam.Publish();

        Assert.Throws<DomainValidationException>(() =>
            exam.Update("New Name", exam.StartAt, exam.EndAt, 20, 3, 1));
    }

    [Fact]
    public void Publish_transitions_from_draft_to_published()
    {
        var exam = CreateValidDraftExam();
        exam.Publish();

        Assert.Equal(ExamStatus.Published, exam.Status);
    }

    [Fact]
    public void Publish_throws_when_already_published_or_non_draft()
    {
        var exam = CreateValidDraftExam();
        exam.Publish();

        Assert.Throws<DomainValidationException>(exam.Publish);
    }

    [Fact]
    public void Start_succeeds_when_published_and_within_time_window()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-10);
        var end = DateTimeOffset.UtcNow.AddMinutes(50);
        var exam = CreateValidDraftExam(start, end);
        exam.Publish();

        exam.Start(DateTimeOffset.UtcNow);

        Assert.Equal(ExamStatus.Ongoing, exam.Status);
    }

    [Fact]
    public void Start_throws_when_not_in_published_state()
    {
        var exam = CreateValidDraftExam();

        Assert.Throws<DomainValidationException>(() => exam.Start(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Start_throws_when_too_early_or_too_late()
    {
        var start = DateTimeOffset.UtcNow.AddHours(2);
        var end = start.AddHours(2);
        var exam = CreateValidDraftExam(start, end);
        exam.Publish();

        // Too early
        Assert.Throws<DomainValidationException>(() => exam.Start(DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => exam.Start(exam.EndAt));

        // Too late
        var pastStart = DateTimeOffset.UtcNow.AddHours(-3);
        var pastEnd = DateTimeOffset.UtcNow.AddHours(-1);
        var pastExam = CreateValidDraftExam(pastStart, pastEnd);
        pastExam.Publish();

        Assert.Throws<DomainValidationException>(() => pastExam.Start(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Complete_transitions_from_ongoing_to_completed()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-10);
        var end = DateTimeOffset.UtcNow.AddMinutes(50);
        var exam = CreateValidDraftExam(start, end);
        exam.Publish();
        exam.Start(DateTimeOffset.UtcNow);

        exam.Complete();

        Assert.Equal(ExamStatus.Completed, exam.Status);
    }

    [Fact]
    public void Complete_throws_when_not_ongoing()
    {
        var exam = CreateValidDraftExam();
        Assert.Throws<DomainValidationException>(exam.Complete);

        exam.Publish();
        Assert.Throws<DomainValidationException>(exam.Complete);
    }

    [Fact]
    public void Cancel_transitions_from_draft_published_or_ongoing()
    {
        var draft = CreateValidDraftExam();
        draft.Cancel();
        Assert.Equal(ExamStatus.Cancelled, draft.Status);

        var pub = CreateValidDraftExam();
        pub.Publish();
        pub.Cancel();
        Assert.Equal(ExamStatus.Cancelled, pub.Status);

        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        var end = DateTimeOffset.UtcNow.AddMinutes(55);
        var ongoing = CreateValidDraftExam(start, end);
        ongoing.Publish();
        ongoing.Start(DateTimeOffset.UtcNow);
        ongoing.Cancel();
        Assert.Equal(ExamStatus.Cancelled, ongoing.Status);
    }

    [Fact]
    public void Cancel_throws_when_already_completed_or_cancelled()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);
        var end = DateTimeOffset.UtcNow.AddMinutes(55);
        var exam = CreateValidDraftExam(start, end);
        exam.Publish();
        exam.Start(DateTimeOffset.UtcNow);
        exam.Complete();

        Assert.Throws<DomainValidationException>(exam.Cancel);

        var cancelled = CreateValidDraftExam();
        cancelled.Cancel();
        Assert.Throws<DomainValidationException>(cancelled.Cancel);
    }

}

[Collection("SQL Server")]
public sealed class ExamLifecycleSqlTests(SqlServerFixture fixture)
{
    private sealed record Actor(Guid Id, bool IsAdministrator = false, bool IsLecturer = true) : ICurrentUser;
    private sealed class Notifier(Func<Guid, Task>? afterCommit = null) : IExamNotifier
    {
        public List<Guid> Events { get; } = [];
        public async Task NotifyChangedAsync(Guid examId, CancellationToken ct)
        {
            Events.Add(examId);
            if (afterCommit is not null) await afterCommit(examId);
        }
    }

    private ApplicationDbContext Context(ExamDemo demo, Actor? actor = null) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(fixture.ConnectionString).Options,
        actor ?? new Actor(demo.Lecturer.Id));

    private static ExamService Service(ApplicationDbContext db, ExamDemo demo, Notifier notifier,
        IExamFoundationReader? reader = null)
    {
        var actor = new Actor(demo.Lecturer.Id);
        var guard = new ExamAccessGuard(new ExamFoundationStore(db), actor);
        var uow = new ExamUnitOfWork(db, notifier, NullLogger<ExamUnitOfWork>.Instance);
        return new(new ExamRepository(db), reader ?? new ExamFoundationStore(db),
            guard, actor, uow, new ExamScheduleService(new ExamScheduleStore(db), guard, uow, actor,
                new ExamFoundationStore(db)));
    }

    private async Task<ExamDemo> SeedAsync(bool ready = false)
    {
        await using var db = fixture.CreateContext();
        var demo = await ExamDemo.SeedAsync(db);
        if (ready)
        {
            await AddEligiblePoolAsync(db, demo);
            demo.Enrollment.Schedule(demo.Exam.StartAt);
            await db.SaveChangesAsync();
        }
        return demo;
    }

    private static async Task AddEligiblePoolAsync(ApplicationDbContext db, ExamDemo demo)
    {
        for (var i = 0; i < demo.Exam.NumberOfMainQuestions; i++)
        {
            var question = new Question(demo.Subject.Id, "Eligible " + i, BloomLevel.Understand,
                QuestionDifficulty.Medium) { CreatedBy = demo.Lecturer.Id };
            question.Approve();
            db.AddRange(question, new ExamQuestionPool(demo.Exam.Id, question.Id),
                new Rubric(question.Id, "Rubric", null, [new CriterionDefinition(null, "Reasoning", null, null, 10, 1)]));
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Service_Publish_rejects_when_readiness_conditions_fail()
    {
        var demo = await SeedAsync();
        await using var db = Context(demo);
        var observedCommit = false;
        var notifier = new Notifier(async id =>
        {
            await using var verify = fixture.CreateContext();
            observedCommit = db.Database.CurrentTransaction is null &&
                await verify.Exams.AnyAsync(e => e.Id == id && e.Status == ExamStatus.Published);
        });
        var service = Service(db, demo, notifier);
        var version = demo.Exam.RowVersion;
        Task Publish() => service.TransitionAsync(demo.Exam.Id, ExamStatus.Published, version, default);

        // An insufficient eligible pool must prevent publication.
        await Assert.ThrowsAsync<DomainValidationException>(Publish);
        await using (var setup = fixture.CreateContext())
        {
            await AddEligiblePoolAsync(setup, demo);
            await setup.ExamStudents.Where(e => e.Id == demo.Enrollment.Id).ExecuteDeleteAsync();
        }
        // An empty roster must prevent publication, even with a valid pool.
        await Assert.ThrowsAsync<DomainValidationException>(Publish);
        var entry = new ExamStudent(demo.Exam.Id, demo.Student.Id);
        await using (var setup = fixture.CreateContext()) { setup.Add(entry); await setup.SaveChangesAsync(); }
        await Assert.ThrowsAsync<DomainValidationException>(Publish);

        // A non-null start is insufficient: the whole slot must fit in the exam window.
        await using (var setup = fixture.CreateContext())
        {
            (await setup.ExamStudents.SingleAsync(e => e.Id == entry.Id)).Schedule(demo.Exam.EndAt.AddMinutes(-14));
            await setup.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<DomainValidationException>(Publish);
        Assert.Empty(notifier.Events);
        await using (var setup = fixture.CreateContext())
        {
            Assert.Equal(ExamStatus.Draft, (await setup.Exams.SingleAsync(e => e.Id == demo.Exam.Id)).Status);
            (await setup.ExamStudents.SingleAsync(e => e.Id == entry.Id)).Schedule(demo.Exam.StartAt);
            await setup.SaveChangesAsync();
        }

        // An edit can invalidate an already saved schedule without moving its starts.
        var nextStudent = new User(Guid.NewGuid() + "@example.test", "unused", "Next", UserStatus.Active);
        var nextEntry = new ExamStudent(demo.Exam.Id, nextStudent.Id);
        nextEntry.Schedule(demo.Exam.StartAt.AddMinutes(15));
        await using (var setup = fixture.CreateContext())
        {
            setup.AddRange(nextStudent, nextEntry);
            await setup.SaveChangesAsync();
        }
        await service.UpdateAsync(demo.Exam.Id, new ExamConfiguration(demo.Subject.Id, demo.Exam.Name,
            demo.Exam.StartAt, demo.Exam.EndAt, 30, demo.Exam.NumberOfMainQuestions,
            demo.Exam.MaxFollowUpPerQuestion), version, default);
        version = (await service.GetAsync(demo.Exam.Id, default)).RowVersion;
        await Assert.ThrowsAsync<DomainValidationException>(Publish);
        Assert.Equal(new[] { demo.Exam.Id }, notifier.Events); // Only the successful draft edit notified.
        notifier.Events.Clear();
        await using (var setup = fixture.CreateContext())
        {
            (await setup.ExamStudents.SingleAsync(e => e.Id == nextEntry.Id)).Schedule(demo.Exam.StartAt.AddMinutes(30));
            await setup.SaveChangesAsync();
        }
        await Publish();
        Assert.True(observedCommit);
        Assert.Equal(new[] { demo.Exam.Id }, notifier.Events);
    }

    private sealed class LockedFoundationReader(ApplicationDbContext db, Func<Task> probe) : IExamFoundationReader
    {
        private readonly ExamFoundationStore inner = new(db);
        public Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct) => inner.ListManagedAsync(ct);
        public async Task<ExamFoundationCounts> CountsAsync(Guid examId, CancellationToken ct)
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            await probe();
            return await inner.CountsAsync(examId, ct);
        }
    }

    [Fact]
    public async Task Service_Publish_holds_exam_lock_during_readiness_validation()
    {
        var demo = await SeedAsync(ready: true);
        await using var db = Context(demo);
        var probed = false;
        var reader = new LockedFoundationReader(db, async () =>
        {
            await using var other = fixture.CreateContext();
            var error = await Assert.ThrowsAsync<SqlException>(() => other.Database.ExecuteSqlInterpolatedAsync(
                $"SET LOCK_TIMEOUT 0; UPDATE [Exams] SET [Name] = [Name] WHERE [Id] = {demo.Exam.Id}"));
            Assert.Equal(1222, error.Number);
            probed = true;
        });
        await Service(db, demo, new Notifier(), reader).TransitionAsync(
            demo.Exam.Id, ExamStatus.Published, demo.Exam.RowVersion, default);
        Assert.True(probed);
    }

    [Fact]
    public async Task Service_Delete_rejects_non_draft_exam()
    {
        var demo = await SeedAsync();
        await using (var setup = fixture.CreateContext())
        {
            (await setup.Exams.SingleAsync(e => e.Id == demo.Exam.Id)).Publish();
            await setup.SaveChangesAsync();
        }
        await using var db = Context(demo);
        var notifier = new Notifier();
        await Assert.ThrowsAsync<DomainValidationException>(() => Service(db, demo, notifier)
            .DeleteDraftAsync(demo.Exam.Id, [], default));
        Assert.Null((await db.Exams.SingleAsync(e => e.Id == demo.Exam.Id)).DeletedAt);
        Assert.Empty(notifier.Events);
    }

    [Fact]
    public async Task Service_Delete_removes_draft_exam()
    {
        var demo = await SeedAsync();
        await using var db = Context(demo);
        var notifier = new Notifier();
        await Service(db, demo, notifier).DeleteDraftAsync(demo.Exam.Id, demo.Exam.RowVersion, default);
        await using var verify = fixture.CreateContext();
        Assert.NotNull((await verify.Exams.SingleAsync(e => e.Id == demo.Exam.Id)).DeletedAt);
        Assert.Equal(new[] { demo.Exam.Id }, notifier.Events);
    }

    [Fact]
    public async Task Service_Update_rejects_subject_change()
    {
        var demo = await SeedAsync();
        var actor = new Actor(Guid.NewGuid(), IsAdministrator: true, IsLecturer: false);
        await using var db = Context(demo, actor);
        var notifier = new Notifier();
        var guard = new ExamAccessGuard(new ExamFoundationStore(db), actor);
        var uow = new ExamUnitOfWork(db, notifier, NullLogger<ExamUnitOfWork>.Instance);
        var service = new ExamService(new ExamRepository(db), new ExamFoundationStore(db),
            guard, actor, uow, new ExamScheduleService(new ExamScheduleStore(db), guard, uow, actor,
                new ExamFoundationStore(db)));
        var request = new ExamConfiguration(Guid.NewGuid(), demo.Exam.Name, demo.Exam.StartAt, demo.Exam.EndAt,
            demo.Exam.DurationMinutes, demo.Exam.NumberOfMainQuestions, demo.Exam.MaxFollowUpPerQuestion);
        await Assert.ThrowsAsync<DomainValidationException>(() => service.UpdateAsync(
            demo.Exam.Id, request, demo.Exam.RowVersion, default));
        Assert.Equal(demo.Subject.Id, (await db.Exams.SingleAsync(e => e.Id == demo.Exam.Id)).SubjectId);
        Assert.Empty(notifier.Events);
    }
}
