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

// Reusable isolated demo fixture: no credentials and no production data.
internal sealed record ExamDemo(User Lecturer, User Student, Subject Subject, Exam Exam, ExamStudent Enrollment, Question Question)
{
    public static async Task<ExamDemo> SeedAsync(ApplicationDbContext db)
    {
        var lecturer = new User(Guid.NewGuid()+"@example.test", "unused-test-hash", "Lecturer", UserStatus.Active);
        lecturer.AssignRole(UserRole.LECTURER);
        var student = new User(Guid.NewGuid()+"@example.test", "unused-test-hash", "Student", UserStatus.Active);
        var subject = new Subject("G2"+Guid.NewGuid().ToString("N"), "Foundation test");
        var exam = new Exam(subject.Id, lecturer.Id, "Demo exam", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(3), 15, 2, 1);
        var entry = new ExamStudent(exam.Id, student.Id);
        var question = new Question(subject.Id, "Explain MVC", BloomLevel.Understand, QuestionDifficulty.Medium) { CreatedBy = lecturer.Id };
        db.AddRange(lecturer, student, subject, exam, entry, question, new LecturerSubject(lecturer.Id, subject.Id));
        await db.SaveChangesAsync();
        return new(lecturer, student, subject, exam, entry, question);
    }
}

[Collection("SQL Server")]
public class ExamFoundationTests(SqlServerFixture fixture)
{
    private sealed record Actor(Guid Id, bool IsAdministrator = false, bool IsLecturer = false) : ICurrentUser;
    private ApplicationDbContext Scoped(ICurrentUser actor) => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(fixture.ConnectionString).Options, actor);
    private async Task<ExamDemo> SeedAsync() { await using var db = fixture.CreateContext(); return await ExamDemo.SeedAsync(db); }

    [Fact]
    public async Task GuardEnforcesOwnerAssignmentAndStudentOwnEnrollment()
    {
        var demo = await SeedAsync();
        await using var db = fixture.CreateContext();
        var store = new ExamFoundationStore(db);
        var owner = new ExamAccessGuard(store, new Actor(demo.Lecturer.Id, IsLecturer:true));
        await owner.CanManageExamAsync(demo.Exam.Id, default);
        var other = new ExamAccessGuard(store, new Actor(Guid.NewGuid(), IsLecturer:true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => other.CanManageExamAsync(demo.Exam.Id, default));
        var student = new ExamAccessGuard(store, new Actor(demo.Student.Id));
        await student.OwnExamStudentAsync(demo.Enrollment.Id, default);
        await student.CanReadExamAsync(demo.Exam.Id, default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => student.CanManageExamAsync(demo.Exam.Id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => other.OwnExamStudentAsync(demo.Enrollment.Id, default));
        await new ExamAccessGuard(store, new Actor(Guid.NewGuid(), IsAdministrator:true)).CanManageExamAsync(demo.Exam.Id, default);
        db.LecturerSubjects.Remove(await db.LecturerSubjects.SingleAsync(x => x.LecturerId == demo.Lecturer.Id)); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => owner.CanManageExamAsync(demo.Exam.Id, default));
    }
    [Fact]
    public async Task QueryFiltersScopeManagerAndStudentAndHideInternalSnapshots()
    {
        var demo = await SeedAsync();
        await using (var setup = fixture.CreateContext())
        {
            var session = new VivaSession(demo.Enrollment.Id, DateTimeOffset.UtcNow);
            setup.AddRange(session, new SessionQuestion(session.Id, demo.Question.Id, 1, demo.Question.Content, "{\"score\":10}"));
            await setup.SaveChangesAsync();
        }
        await using (var owner = Scoped(new Actor(demo.Lecturer.Id, IsLecturer:true)))
        { Assert.Single(await owner.Exams.ToArrayAsync()); Assert.Single(await owner.SessionQuestions.ToArrayAsync()); }
        await using (var stranger = Scoped(new Actor(Guid.NewGuid(), IsLecturer:true)))
        { Assert.Empty(await stranger.Exams.ToArrayAsync()); Assert.Empty(await stranger.ExamStudents.ToArrayAsync()); Assert.Empty(await stranger.SessionQuestions.ToArrayAsync()); }
        await using var student = Scoped(new Actor(demo.Student.Id));
        Assert.Single(await student.ExamStudents.ToArrayAsync()); Assert.Single(await student.VivaSessions.ToArrayAsync());
        Assert.Empty(await student.Exams.ToArrayAsync()); Assert.Empty(await student.SessionQuestions.ToArrayAsync());
    }
    [Theory]
    [InlineData("pool")]
    [InlineData("roster")]
    [InlineData("session")]
    [InlineData("position")]
    [InlineData("question")]
    public async Task DatabaseRejectsDuplicateFoundationRows(string kind)
    {
        var d = await SeedAsync(); await using var db = fixture.CreateContext();
        var session = new VivaSession(d.Enrollment.Id, DateTimeOffset.UtcNow);
        var otherQuestion = new Question(d.Subject.Id, "Other question", BloomLevel.Understand, QuestionDifficulty.Medium);
        db.AddRange(otherQuestion, session, new ExamQuestionPool(d.Exam.Id, d.Question.Id), new SessionQuestion(session.Id,d.Question.Id,1,"Question","{}"));
        await db.SaveChangesAsync();
        object duplicate = kind switch
        {
            "pool" => new ExamQuestionPool(d.Exam.Id,d.Question.Id),
            "roster" => new ExamStudent(d.Exam.Id,d.Student.Id),
            "session" => new VivaSession(d.Enrollment.Id, DateTimeOffset.UtcNow),
            "position" => new SessionQuestion(session.Id,otherQuestion.Id,1,"Other","{}"),
            _ => new SessionQuestion(session.Id,d.Question.Id,2,"Same","{}")
        };
        db.Add(duplicate);
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sql = Assert.IsType<Microsoft.Data.SqlClient.SqlException>(failure.InnerException);
        Assert.Contains(sql.Number, new[] { 2601, 2627 });
    }
    private sealed class Recorder(Func<Guid, Task>? onNotify = null) : IExamNotifier
    {
        public List<Guid> Events { get; } = [];
        public async Task NotifyChangedAsync(Guid id, CancellationToken ct) { Events.Add(id); if (onNotify != null) await onNotify(id); }
    }
    [Fact]
    public async Task NotificationsObserveCommittedDataAndFailuresDoNotUndoCommit()
    {
        var d = await SeedAsync(); await using var db = fixture.CreateContext();
        var observedCommittedData = false;
        var notifier = new Recorder(async id => {
            await using var verify = fixture.CreateContext();
            observedCommittedData = db.Database.CurrentTransaction is null && await verify.ExamQuestionPools.AnyAsync(p => p.ExamId == id);
            throw new InvalidOperationException("Simulated disconnect");
        });
        var uow = new ExamUnitOfWork(db, notifier, NullLogger<ExamUnitOfWork>.Instance);
        await uow.ExecuteAsync(_ => { db.Add(new ExamQuestionPool(d.Exam.Id, d.Question.Id)); return Task.CompletedTask; }, [d.Exam.Id, d.Exam.Id], default);
        Assert.Single(notifier.Events);
        Assert.True(observedCommittedData);
        await using var final = fixture.CreateContext(); Assert.True(await final.ExamQuestionPools.AnyAsync(p => p.ExamId == d.Exam.Id));
    }
    [Fact]
    public async Task FailedWorkRollsBackAndNeverNotifies()
    {
        var d = await SeedAsync(); await using var db = fixture.CreateContext(); var notifier = new Recorder();
        var uow = new ExamUnitOfWork(db, notifier, NullLogger<ExamUnitOfWork>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteAsync(async ct => {
            db.Add(new ExamQuestionPool(d.Exam.Id, d.Question.Id)); await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Rollback");
        }, [d.Exam.Id], default));
        Assert.Empty(notifier.Events);
        await using var verify = fixture.CreateContext(); Assert.False(await verify.ExamQuestionPools.AnyAsync(p => p.ExamId == d.Exam.Id));
    }
    [Fact]
    public async Task StaleRosterUpdateReturnsConflictAndDoesNotNotify()
    {
        var d = await SeedAsync(); await using var first = fixture.CreateContext(); await using var second = fixture.CreateContext();
        var a = await first.ExamStudents.SingleAsync(x => x.Id == d.Enrollment.Id);
        var b = await second.ExamStudents.SingleAsync(x => x.Id == d.Enrollment.Id);
        a.Schedule(d.Exam.StartAt); await first.SaveChangesAsync(); b.Schedule(d.Exam.StartAt.AddMinutes(15));
        var notify = new Recorder(); var uow = new ExamUnitOfWork(second, notify, NullLogger<ExamUnitOfWork>.Instance);
        await Assert.ThrowsAsync<ResourceConflictException>(() => uow.ExecuteAsync(_ => Task.CompletedTask, [d.Exam.Id], default));
        Assert.Empty(notify.Events);
    }
    [Fact]
    public void ScaffoldValidatesConfigurationAndSafeStudentDtoHasNoRubric()
    {
        Assert.Throws<DomainValidationException>(() => new Exam(Guid.NewGuid(), Guid.NewGuid(), "Demo", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), 15, 2, 1));
        Assert.Throws<DomainValidationException>(() => new SessionQuestion(Guid.NewGuid(), Guid.NewGuid(), 1, "Question", "not-json"));
        Assert.Null(typeof(StudentQuestion).GetProperty("RubricSnapshot"));
        Assert.Null(typeof(StudentQuestion).GetProperty("ExpectedAnswer"));
    }
}
