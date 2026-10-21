using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.Scheduling;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using AIpoweredVivaExamSystem.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public class SequentialScheduleTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 20, 8, 0, 0, TimeSpan.FromHours(7));
    private static ScheduleEnrollment Entry(int order) => new(Guid.NewGuid(),
        Guid.Parse($"00000000-0000-0000-0000-{order:D12}"), "Student", "student@example.test", null, []);

    [Fact]
    public void SameRosterInAnyOrderProducesTheSameSequentialUtcSlots()
    {
        var roster = new[] { Entry(3), Entry(1), Entry(2) };
        var slots = SequentialSchedule.Generate(Start, Start.AddHours(1), 15, roster);
        Assert.Equal(slots, SequentialSchedule.Generate(Start, Start.AddHours(1), 15, roster.Reverse().ToArray()));
        Assert.Equal(roster.OrderBy(x => x.StudentId).Select(x => x.EnrollmentId), slots.Select(x => x.EnrollmentId));
        for (var i = 0; i < slots.Count; i++)
        {
            Assert.Equal(Start.AddMinutes(i * 15), slots[i].StartAt);
            Assert.Equal(TimeSpan.Zero, slots[i].StartAt.Offset);
            Assert.Equal(TimeSpan.FromMinutes(15), slots[i].EndAt - slots[i].StartAt);
        }
    }

    [Fact]
    public void LastSlotCanEndExactlyAtExamEnd()
    {
        var slots = SequentialSchedule.Generate(Start, Start.AddMinutes(30), 15, [Entry(1), Entry(2)]);
        Assert.Equal(Start.AddMinutes(30), slots[^1].EndAt);
    }

    [Fact]
    public void InsufficientWindowRejectsTheWholeRoster() =>
        Assert.Throws<DomainValidationException>(() =>
            SequentialSchedule.Generate(Start, Start.AddMinutes(29), 15, [Entry(1), Entry(2)]));

    [Fact]
    public void EmptyRosterCannotProduceAPreview() =>
        Assert.Throws<DomainValidationException>(() => SequentialSchedule.Generate(Start, Start.AddHours(1), 15, []));
}

[Collection("SQL Server")]
public class ExamScheduleTests(SqlServerFixture fixture)
{
    private sealed record Actor(Guid Id, bool IsAdministrator = false, bool IsLecturer = false) : ICurrentUser;
    private sealed class Recorder(Func<Guid, Task>? observe = null) : IExamNotifier
    {
        public List<Guid> Events { get; } = [];
        public async Task NotifyChangedAsync(Guid id, CancellationToken ct)
        {
            Events.Add(id);
            if (observe is not null) await observe(id);
        }
    }

    private ApplicationDbContext Scoped(ICurrentUser actor) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(fixture.ConnectionString).Options, actor);
    private static Actor Owner(ExamDemo demo) => new(demo.Lecturer.Id, IsLecturer: true);
    private static ExamScheduleService Service(ApplicationDbContext db, ICurrentUser actor, Recorder? notifier = null) =>
        new(new ExamScheduleStore(db), new ExamAccessGuard(new ExamFoundationStore(db), actor),
            new ExamUnitOfWork(db, notifier ?? new Recorder(), NullLogger<ExamUnitOfWork>.Instance), actor,
            new ExamFoundationStore(db));
    private async Task<ExamDemo> SeedAsync(int students = 1)
    {
        await using var db = fixture.CreateContext();
        var demo = await ExamDemo.SeedAsync(db);
        for (var i = 1; i < students; i++)
        {
            var student = new User(Guid.NewGuid() + "@example.test", "unused-test-hash", "Other student", UserStatus.Active);
            db.AddRange(student, new ExamStudent(demo.Exam.Id, student.Id));
        }
        await db.SaveChangesAsync();
        return demo;
    }

    [Fact]
    public async Task ApplyPersistsEverySlotAndNotifierObservesOnlyCommittedData()
    {
        var demo = await SeedAsync(3);
        await using var db = Scoped(Owner(demo));
        var observedCommitted = false;
        var notify = new Recorder(async id =>
        {
            await using var verify = fixture.CreateContext();
            observedCommitted = db.Database.CurrentTransaction is null &&
                await verify.ExamStudents.CountAsync(x => x.ExamId == id && x.ScheduledAt != null) == 3;
        });
        var service = Service(db, Owner(demo), notify);
        Assert.False(await service.IsFullyScheduledAsync(demo.Exam.Id, default));
        var preview = await service.PreviewAsync(demo.Exam.Id, default);
        Assert.Empty(notify.Events);
        await service.ApplyAsync(demo.Exam.Id, preview.PreviewToken, default);
        Assert.Equal([demo.Exam.Id], notify.Events);
        Assert.True(observedCommitted);
        Assert.True(await service.IsFullyScheduledAsync(demo.Exam.Id, default));
        await using var final = fixture.CreateContext();
        var saved = await final.ExamStudents.Where(x => x.ExamId == demo.Exam.Id).ToDictionaryAsync(x => x.Id);
        Assert.All(preview.Slots, slot => Assert.Equal(slot.StartAt, saved[slot.EnrollmentId].ScheduledAt));
        Assert.NotEqual(demo.Exam.RowVersion, (await final.Exams.SingleAsync(x => x.Id == demo.Exam.Id)).RowVersion);
    }

    [Fact]
    public async Task CompetingAppliesOfTheSamePreviewHaveExactlyOneWinner()
    {
        var demo = await SeedAsync();
        await using var first = Scoped(Owner(demo));
        await using var second = Scoped(Owner(demo));
        var firstNotify = new Recorder();
        var secondNotify = new Recorder();
        var a = Service(first, Owner(demo), firstNotify);
        var b = Service(second, Owner(demo), secondNotify);
        var preview = await a.PreviewAsync(demo.Exam.Id, default);
        var results = await Task.WhenAll(
            Record.ExceptionAsync(() => a.ApplyAsync(demo.Exam.Id, preview.PreviewToken, default)),
            Record.ExceptionAsync(() => b.ApplyAsync(demo.Exam.Id, preview.PreviewToken, default)));
        Assert.Single(results, x => x is null);
        Assert.IsType<ResourceConflictException>(Assert.Single(results, x => x is not null));
        Assert.Equal(1, firstNotify.Events.Count + secondNotify.Events.Count);
    }

    [Fact]
    public async Task EnrollmentVersionChangeInvalidatesPreviewEvenWithoutExamVersionChange()
    {
        var demo = await SeedAsync();
        await using var db = Scoped(Owner(demo));
        var notify = new Recorder();
        var service = Service(db, Owner(demo), notify);
        var preview = await service.PreviewAsync(demo.Exam.Id, default);
        var moved = demo.Exam.StartAt.AddMinutes(15);
        await using (var update = fixture.CreateContext())
        {
            (await update.ExamStudents.SingleAsync(x => x.Id == demo.Enrollment.Id)).Schedule(moved);
            await update.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.ApplyAsync(demo.Exam.Id, preview.PreviewToken, default));
        Assert.Empty(notify.Events);
        await using var verify = fixture.CreateContext();
        Assert.Equal(moved, (await verify.ExamStudents.SingleAsync(x => x.Id == demo.Enrollment.Id)).ScheduledAt);
    }

    [Fact]
    public async Task RemovingLastEnrollmentAfterPreviewReturnsStalePreviewConflict()
    {
        var demo = await SeedAsync();
        await using var db = Scoped(Owner(demo));
        var service = Service(db, Owner(demo));
        var preview = await service.PreviewAsync(demo.Exam.Id, default);
        await using (var update = fixture.CreateContext())
        {
            update.ExamStudents.Remove(await update.ExamStudents.SingleAsync(x => x.Id == demo.Enrollment.Id));
            await update.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.ApplyAsync(demo.Exam.Id, preview.PreviewToken, default));
    }

    [Fact]
    public async Task StudentConflictInAnotherLecturersExamRollsBackEverySlotAndNeverNotifies()
    {
        var demo = await SeedAsync(3);
        var other = await SeedAsync();
        await using (var setup = fixture.CreateContext())
        {
            var roster = await setup.ExamStudents.Where(x => x.ExamId == demo.Exam.Id).ToArrayAsync();
            var last = roster.OrderBy(x => x.StudentId).Last();
            var conflictingExam = new Exam(other.Subject.Id, other.Lecturer.Id, "Hidden conflicting exam",
                demo.Exam.StartAt, demo.Exam.EndAt, 15, 2, 1);
            var conflictingEntry = new ExamStudent(conflictingExam.Id, last.StudentId);
            conflictingEntry.Schedule(demo.Exam.StartAt.AddMinutes(30));
            setup.AddRange(conflictingExam, conflictingEntry);
            await setup.SaveChangesAsync();
        }
        await using var db = Scoped(Owner(demo));
        var notify = new Recorder();
        var service = Service(db, Owner(demo), notify);
        var preview = await service.PreviewAsync(demo.Exam.Id, default);
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.ApplyAsync(demo.Exam.Id, preview.PreviewToken, default));
        Assert.Empty(notify.Events);
        await using var verify = fixture.CreateContext();
        Assert.All(await verify.ExamStudents.Where(x => x.ExamId == demo.Exam.Id).ToArrayAsync(), x => Assert.Null(x.ScheduledAt));
        Assert.Equal(demo.Exam.RowVersion, (await verify.Exams.SingleAsync(x => x.Id == demo.Exam.Id)).RowVersion);
    }

    [Fact]
    public async Task LecturerCannotBeScheduledInTwoExamsAtOnce()
    {
        var demo = await SeedAsync();
        var other = await SeedAsync();
        await using (var setup = fixture.CreateContext())
        {
            var conflict = new Exam(demo.Subject.Id, demo.Lecturer.Id, "Lecturer conflict",
                demo.Exam.StartAt, demo.Exam.EndAt, 15, 2, 1);
            var entry = new ExamStudent(conflict.Id, other.Student.Id);
            entry.Schedule(demo.Exam.StartAt);
            setup.AddRange(conflict, entry);
            await setup.SaveChangesAsync();
        }
        await using var db = Scoped(Owner(demo));
        var service = Service(db, Owner(demo));
        var preview = await service.PreviewAsync(demo.Exam.Id, default);
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.ApplyAsync(demo.Exam.Id, preview.PreviewToken, default));
    }

    [Fact]
    public async Task AdjacentSlotsDoNotOverlap()
    {
        var demo = await SeedAsync();
        await using (var setup = fixture.CreateContext())
        {
            var adjacent = new Exam(demo.Subject.Id, demo.Lecturer.Id, "Adjacent exam",
                demo.Exam.StartAt.AddMinutes(-15), demo.Exam.EndAt, 15, 2, 1);
            var entry = new ExamStudent(adjacent.Id, demo.Student.Id);
            entry.Schedule(adjacent.StartAt);
            setup.AddRange(adjacent, entry);
            await setup.SaveChangesAsync();
        }
        await using var db = Scoped(Owner(demo));
        var service = Service(db, Owner(demo));
        var preview = await service.PreviewAsync(demo.Exam.Id, default);
        await service.ApplyAsync(demo.Exam.Id, preview.PreviewToken, default);
    }

    [Fact]
    public async Task CancelledExamsDoNotReserveStudentOrLecturerSlots()
    {
        var demo = await SeedAsync();
        await using (var setup = fixture.CreateContext())
        {
            var cancelled = new Exam(demo.Subject.Id, demo.Lecturer.Id, "Cancelled exam",
                demo.Exam.StartAt, demo.Exam.EndAt, 15, 2, 1);
            var entry = new ExamStudent(cancelled.Id, demo.Student.Id);
            entry.Schedule(cancelled.StartAt);
            setup.AddRange(cancelled, entry);
            setup.Entry(cancelled).Property(x => x.Status).CurrentValue = ExamStatus.Cancelled;
            await setup.SaveChangesAsync();
        }
        await using var db = Scoped(Owner(demo));
        var service = Service(db, Owner(demo));
        var preview = await service.PreviewAsync(demo.Exam.Id, default);
        await service.ApplyAsync(demo.Exam.Id, preview.PreviewToken, default);
    }

    [Fact]
    public async Task CrossExamConcurrentAppliesCannotDoubleBookASharedStudent()
    {
        var a = await SeedAsync();
        var b = await SeedAsync();
        Guid secondExamId;
        await using (var setup = fixture.CreateContext())
        {
            var exam = new Exam(b.Subject.Id, b.Lecturer.Id, "Shared student",
                a.Exam.StartAt, a.Exam.EndAt, 15, 2, 1);
            secondExamId = exam.Id;
            setup.AddRange(exam, new ExamStudent(exam.Id, a.Student.Id));
            await setup.SaveChangesAsync();
        }
        await using var first = Scoped(Owner(a));
        await using var second = Scoped(Owner(b));
        var serviceA = Service(first, Owner(a));
        var serviceB = Service(second, Owner(b));
        var previewA = await serviceA.PreviewAsync(a.Exam.Id, default);
        var previewB = await serviceB.PreviewAsync(secondExamId, default);
        var results = await Task.WhenAll(
            Record.ExceptionAsync(() => serviceA.ApplyAsync(a.Exam.Id, previewA.PreviewToken, default)),
            Record.ExceptionAsync(() => serviceB.ApplyAsync(secondExamId, previewB.PreviewToken, default)));
        Assert.Single(results, x => x is null);
        Assert.IsType<ResourceConflictException>(Assert.Single(results, x => x is not null));
        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.ExamStudents.CountAsync(x =>
            (x.ExamId == a.Exam.Id || x.ExamId == secondExamId) && x.ScheduledAt != null));
    }

    [Fact]
    public async Task AnotherLecturerCannotPreviewOrApplyOrReadTheExam()
    {
        var demo = await SeedAsync();
        var stranger = new Actor(Guid.NewGuid(), IsLecturer: true);
        await using var db = Scoped(stranger);
        var service = Service(db, stranger);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PreviewAsync(demo.Exam.Id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApplyAsync(demo.Exam.Id, "", default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(demo.Exam.Id, default));
    }

    [Fact]
    public async Task PublishedExamRejectsPreviewAndApply()
    {
        var demo = await SeedAsync();
        await using var db = Scoped(Owner(demo));
        var service = Service(db, Owner(demo));
        var preview = await service.PreviewAsync(demo.Exam.Id, default);
        await using (var setup = fixture.CreateContext())
        {
            var exam = await setup.Exams.SingleAsync(x => x.Id == demo.Exam.Id);
            setup.Entry(exam).Property(x => x.Status).CurrentValue = ExamStatus.Published;
            await setup.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.PreviewAsync(demo.Exam.Id, default));
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.ApplyAsync(demo.Exam.Id, preview.PreviewToken, default));
    }

    [Fact]
    public async Task ExistingVivaSessionPreventsRescheduling()
    {
        var demo = await SeedAsync();
        await using var db = Scoped(Owner(demo));
        var service = Service(db, Owner(demo));
        var preview = await service.PreviewAsync(demo.Exam.Id, default);
        await using (var setup = fixture.CreateContext())
        {
            setup.Add(new VivaSession(demo.Enrollment.Id, demo.Exam.StartAt));
            await setup.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.ApplyAsync(demo.Exam.Id, preview.PreviewToken, default));
        await using var verify = fixture.CreateContext();
        Assert.Null((await verify.ExamStudents.SingleAsync(x => x.Id == demo.Enrollment.Id)).ScheduledAt);
    }

    [Fact]
    public async Task MyScheduleExcludesDraftCancelledUnscheduledAndOtherStudentsExams()
    {
        var mine = await SeedAsync();
        var other = await SeedAsync();
        await using (var setup = fixture.CreateContext())
        {
            (await setup.ExamStudents.SingleAsync(x => x.Id == mine.Enrollment.Id)).Schedule(mine.Exam.StartAt);
            (await setup.ExamStudents.SingleAsync(x => x.Id == other.Enrollment.Id)).Schedule(other.Exam.StartAt);
            var published = await setup.Exams.SingleAsync(x => x.Id == mine.Exam.Id);
            setup.Entry(published).Property(x => x.Status).CurrentValue = ExamStatus.Published;
            var otherPublished = await setup.Exams.SingleAsync(x => x.Id == other.Exam.Id);
            setup.Entry(otherPublished).Property(x => x.Status).CurrentValue = ExamStatus.Published;
            var cancelled = new Exam(mine.Subject.Id, mine.Lecturer.Id, "Cancelled",
                mine.Exam.StartAt, mine.Exam.EndAt, 15, 2, 1);
            var unscheduled = new Exam(mine.Subject.Id, mine.Lecturer.Id, "Unscheduled",
                mine.Exam.StartAt, mine.Exam.EndAt, 15, 2, 1);
            var draft = new Exam(mine.Subject.Id, mine.Lecturer.Id, "Draft",
                mine.Exam.StartAt, mine.Exam.EndAt, 15, 2, 1);
            var draftEntry = new ExamStudent(draft.Id, mine.Student.Id);
            draftEntry.Schedule(draft.StartAt);
            var cancelledEntry = new ExamStudent(cancelled.Id, mine.Student.Id);
            cancelledEntry.Schedule(cancelled.StartAt);
            setup.AddRange(cancelled, cancelledEntry, unscheduled, new ExamStudent(unscheduled.Id, mine.Student.Id),
                draft, draftEntry);
            setup.Entry(cancelled).Property(x => x.Status).CurrentValue = ExamStatus.Cancelled;
            setup.Entry(unscheduled).Property(x => x.Status).CurrentValue = ExamStatus.Published;
            await setup.SaveChangesAsync();
        }
        var actor = new Actor(mine.Student.Id);
        await using var db = Scoped(actor);
        var service = Service(db, actor);
        var slot = Assert.Single(await service.MyScheduleAsync(default));
        Assert.Equal(mine.Enrollment.Id, slot.EnrollmentId);
        Assert.Equal(mine.Exam.StartAt, slot.StartAt);
        Assert.Equal(mine.Exam.StartAt.AddMinutes(mine.Exam.DurationMinutes), slot.EndAt);
        Assert.Equal(mine.Exam.Name, Assert.Single(await service.MyScheduleDetailsAsync(default)).ExamName);
        var vivaExams = await new MyVivaSessionsReader(db, actor).ListAsync(default);
        Assert.Equal(2, vivaExams.Count);
        Assert.Equal(slot.EndAt, vivaExams.Single(x => x.EnrollmentId == mine.Enrollment.Id).EndAt);
        var unscheduledExam = Assert.Single(vivaExams, x => x.EnrollmentId != mine.Enrollment.Id);
        Assert.Null(unscheduledExam.ScheduledAt);
        Assert.Null(unscheduledExam.EndAt);
    }

    [Fact]
    public async Task ReadinessRequiresNonemptyRosterAndNonoverlappingSlotsWithinWindow()
    {
        var demo = await SeedAsync(2);
        await using var db = Scoped(Owner(demo));
        var service = Service(db, Owner(demo));
        await using (var setup = fixture.CreateContext())
        {
            var entries = await setup.ExamStudents.Where(x => x.ExamId == demo.Exam.Id).ToArrayAsync();
            entries[0].Schedule(demo.Exam.StartAt);
            entries[1].Schedule(demo.Exam.EndAt.AddMinutes(-14));
            await setup.SaveChangesAsync();
        }
        Assert.False(await service.IsFullyScheduledAsync(demo.Exam.Id, default));
        await using (var setup = fixture.CreateContext())
        {
            var entries = await setup.ExamStudents.Where(x => x.ExamId == demo.Exam.Id).ToArrayAsync();
            entries[0].Schedule(demo.Exam.StartAt);
            entries[1].Schedule(demo.Exam.StartAt.AddMinutes(15));
            await setup.SaveChangesAsync();
        }
        Assert.True(await service.IsFullyScheduledAsync(demo.Exam.Id, default));
        await using (var setup = fixture.CreateContext())
        {
            var exam = await setup.Exams.SingleAsync(x => x.Id == demo.Exam.Id);
            exam.Update(exam.Name, exam.StartAt, exam.EndAt, 30, exam.NumberOfMainQuestions, exam.MaxFollowUpPerQuestion);
            await setup.SaveChangesAsync();
        }
        Assert.False(await service.IsFullyScheduledAsync(demo.Exam.Id, default));
        await using (var setup = fixture.CreateContext())
        {
            setup.ExamStudents.RemoveRange(await setup.ExamStudents.Where(x => x.ExamId == demo.Exam.Id).ToArrayAsync());
            await setup.SaveChangesAsync();
        }
        Assert.False(await service.IsFullyScheduledAsync(demo.Exam.Id, default));
    }
}
