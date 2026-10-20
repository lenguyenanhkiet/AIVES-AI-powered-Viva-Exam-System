using AIpoweredVivaExamSystem.Application;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

[Collection("SQL Server")]
public sealed class ExamRosterTests(SqlServerFixture fixture)
{
    private sealed record Actor(Guid Id, bool IsAdministrator = false, bool IsLecturer = true) : ICurrentUser;
    private sealed class Notifications(Func<Guid, Task>? onNotify = null) : IExamNotifier
    {
        public List<Guid> Exams { get; } = [];
        public async Task NotifyChangedAsync(Guid examId, CancellationToken ct)
        { Exams.Add(examId); if (onNotify is not null) await onNotify(examId); }
    }

    private ServiceProvider Services(Guid actorId, Notifications? notifications = null, bool lecturer = true, bool admin = false,
        SaveChangesInterceptor? interceptor = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentUser>(new Actor(actorId, admin, lecturer));
        services.AddSingleton<IExamNotifier>(notifications ?? new Notifications());
        services.AddApplication();
        services.AddPersistence(config);
        if (interceptor is not null)
            services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(interceptor));
        return services.BuildServiceProvider();
    }

    private async Task<ExamDemo> SeedAsync()
    {
        await using var db = fixture.CreateContext();
        return await ExamDemo.SeedAsync(db);
    }

    [Fact]
    public async Task AddNormalizesEmailAndPersistsOneEnrollment()
    {
        var demo = await SeedAsync();
        var student = new User(Guid.NewGuid() + "@example.test", "unused", "New student", UserStatus.Active);
        await using (var db = fixture.CreateContext()) { db.Add(student); await db.SaveChangesAsync(); }
        var notifications = new Notifications();
        await using var provider = Services(demo.Lecturer.Id, notifications);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IExamRosterService>();
        await service.AddAsync(demo.Exam.Id, "  " + student.Email.ToUpperInvariant() + "  ", default);
        var roster = await service.ListAsync(demo.Exam.Id, default);
        Assert.Equal(2, roster.Count);
        Assert.Contains(roster, x => x.StudentId == student.Id && x.Name == "New student");
        Assert.Contains(roster, x => x.StudentId == student.Id && x.Email == student.Email);
        await using var verify = fixture.CreateContext();
        Assert.Single(await verify.ExamStudents.Where(x => x.ExamId == demo.Exam.Id && x.StudentId == student.Id).ToArrayAsync());
        var savedExam = await verify.Exams.SingleAsync(x => x.Id == demo.Exam.Id);
        Assert.False(demo.Exam.RowVersion.SequenceEqual(savedExam.RowVersion));
        Assert.Equal(new[] { demo.Exam.Id }, notifications.Exams);
    }

    [Theory]
    [InlineData(UserRole.LECTURER, UserStatus.Active, false)]
    [InlineData(UserRole.ADMIN, UserStatus.Active, false)]
    [InlineData(UserRole.STUDENT, UserStatus.Locked, false)]
    [InlineData(UserRole.STUDENT, UserStatus.Inactive, false)]
    [InlineData(UserRole.STUDENT, UserStatus.Active, true)]
    public async Task AddRejectsNonStudentInactiveOrDeletedAccounts(UserRole role, UserStatus status, bool deleted)
    {
        var d = await SeedAsync();
        var user = new User(Guid.NewGuid() + "@example.test", "unused", "Invalid", status);
        user.AssignRole(role);
        if (deleted) user.DeletedAt = DateTimeOffset.UtcNow;
        await using (var db = fixture.CreateContext()) { db.Add(user); await db.SaveChangesAsync(); }
        var notifications = new Notifications();
        await using var provider = Services(d.Lecturer.Id, notifications);
        await using var scope = provider.CreateAsyncScope();
        await Assert.ThrowsAsync<DomainValidationException>(() => scope.ServiceProvider.GetRequiredService<IExamRosterService>().AddAsync(d.Exam.Id, user.Email, default));
        await using var verify = fixture.CreateContext();
        Assert.False(await verify.ExamStudents.AnyAsync(x => x.StudentId == user.Id));
        Assert.Empty(notifications.Exams);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-email")]
    [InlineData("missing@example.test")]
    public async Task InvalidOrUnknownEmailDoesNotChangeRoster(string email)
    {
        var d = await SeedAsync();
        await using var provider = Services(d.Lecturer.Id);
        await using var scope = provider.CreateAsyncScope();
        await Assert.ThrowsAsync<DomainValidationException>(() => scope.ServiceProvider.GetRequiredService<IExamRosterService>().AddAsync(d.Exam.Id, email, default));
        await using var verify = fixture.CreateContext();
        Assert.Single(await verify.ExamStudents.Where(x => x.ExamId == d.Exam.Id).ToArrayAsync());
    }

    [Fact]
    public async Task DuplicateRequestsReturnConflictAndDoNotNotify()
    {
        var d = await SeedAsync();
        var notifications = new Notifications();
        await using var provider = Services(d.Lecturer.Id, notifications);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IExamRosterService>();
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.AddAsync(d.Exam.Id, d.Student.Email, default));
        Assert.Single(await service.ListAsync(d.Exam.Id, default));
        Assert.Empty(notifications.Exams);
    }

    [Theory]
    [InlineData(ExamStatus.Published)]
    [InlineData(ExamStatus.Ongoing)]
    [InlineData(ExamStatus.Completed)]
    [InlineData(ExamStatus.Cancelled)]
    public async Task NonDraftExamRejectsBothMutations(ExamStatus status)
    {
        var d = await SeedAsync();
        await using (var db = fixture.CreateContext())
            await db.Exams.Where(x => x.Id == d.Exam.Id).ExecuteUpdateAsync(x => x.SetProperty(e => e.Status, status));
        var notifications = new Notifications();
        await using var provider = Services(d.Lecturer.Id, notifications);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IExamRosterService>();
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.AddAsync(d.Exam.Id, d.Student.Email, default));
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.RemoveAsync(d.Exam.Id, d.Enrollment.Id, default));
        Assert.Single(await service.ListAsync(d.Exam.Id, default));
        Assert.Empty(notifications.Exams);
    }

    [Fact]
    public async Task RemovalRejectsSessionAndForeignEnrollmentButRemovesUnusedEntry()
    {
        var d = await SeedAsync();
        var other = await SeedAsync();
        await using var provider = Services(d.Lecturer.Id);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IExamRosterService>();
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => service.RemoveAsync(d.Exam.Id, other.Enrollment.Id, default));
        var session = new VivaSession(d.Enrollment.Id, DateTimeOffset.UtcNow);
        await using (var db = fixture.CreateContext()) { db.Add(session); await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<ResourceConflictException>(() => service.RemoveAsync(d.Exam.Id, d.Enrollment.Id, default));
        await using (var db = fixture.CreateContext()) { await db.VivaSessions.Where(x => x.Id == session.Id).ExecuteDeleteAsync(); }
        await service.RemoveAsync(d.Exam.Id, d.Enrollment.Id, default);
        Assert.Empty(await service.ListAsync(d.Exam.Id, default));
        await using var verify = fixture.CreateContext();
        Assert.True(await verify.ExamStudents.AnyAsync(x => x.Id == other.Enrollment.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StrangerAndStudentCannotReadOrMutateRoster(bool isStudent)
    {
        var d = await SeedAsync();
        await using var provider = Services(isStudent ? d.Student.Id : Guid.NewGuid(), lecturer: !isStudent);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IExamRosterService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListAsync(d.Exam.Id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AddAsync(d.Exam.Id, d.Student.Email, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RemoveAsync(d.Exam.Id, d.Enrollment.Id, default));
    }

    [Fact]
    public async Task ParallelAddCreatesOneRowAndOnlyWinnerNotifies()
    {
        var d = await SeedAsync();
        var user = new User(Guid.NewGuid() + "@example.test", "unused", "Concurrent", UserStatus.Active);
        await using (var db = fixture.CreateContext()) { db.Add(user); await db.SaveChangesAsync(); }
        async Task<(bool Won, int Notifications)> Add()
        {
            var notifications = new Notifications();
            await using var provider = Services(d.Lecturer.Id, notifications);
            await using var scope = provider.CreateAsyncScope();
            try { await scope.ServiceProvider.GetRequiredService<IExamRosterService>().AddAsync(d.Exam.Id, user.Email, default); return (true, notifications.Exams.Count); }
            catch (ResourceConflictException) { return (false, notifications.Exams.Count); }
        }
        var results = await Task.WhenAll(Add(), Add());
        Assert.Single(results, x => x.Won);
        Assert.Equal(1, results.Single(x => x.Won).Notifications);
        Assert.Equal(0, results.Single(x => !x.Won).Notifications);
        await using var verify = fixture.CreateContext();
        Assert.Single(await verify.ExamStudents.Where(x => x.StudentId == user.Id && x.ExamId == d.Exam.Id).ToArrayAsync());
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated failure after SQL save but before transaction commit.");
    }

    [Fact]
    public async Task FailureBeforeCommitRollsBackEnrollmentAndExamVersionWithoutNotification()
    {
        var d = await SeedAsync();
        var user = new User(Guid.NewGuid() + "@example.test", "unused", "Rollback", UserStatus.Active);
        await using (var db = fixture.CreateContext()) { db.Add(user); await db.SaveChangesAsync(); }
        var notifications = new Notifications();
        await using var provider = Services(d.Lecturer.Id, notifications, interceptor: new FailAfterSave());
        await using var scope = provider.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IExamRosterService>()
            .AddAsync(d.Exam.Id, user.Email, default));
        await using var verify = fixture.CreateContext();
        Assert.False(await verify.ExamStudents.AnyAsync(x => x.StudentId == user.Id));
        var exam = await verify.Exams.SingleAsync(x => x.Id == d.Exam.Id);
        Assert.Equal(d.Exam.RowVersion, exam.RowVersion);
        Assert.Empty(notifications.Exams);
    }

    [Fact]
    public async Task NotificationReadsCommittedRosterAndDeliveryFailureDoesNotUndoEnrollment()
    {
        var d = await SeedAsync();
        var user = new User(Guid.NewGuid() + "@example.test", "unused", "Committed", UserStatus.Active);
        await using (var db = fixture.CreateContext()) { db.Add(user); await db.SaveChangesAsync(); }
        var observedCommit = false;
        var notifications = new Notifications(async id =>
        {
            await using var db = fixture.CreateContext();
            observedCommit = await db.ExamStudents.AnyAsync(x => x.ExamId == id && x.StudentId == user.Id);
            throw new InvalidOperationException("Simulated SignalR disconnection");
        });
        await using var provider = Services(d.Lecturer.Id, notifications);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IExamRosterService>().AddAsync(d.Exam.Id, user.Email, default);
        Assert.True(observedCommit);
        Assert.Equal(new[] { d.Exam.Id }, notifications.Exams);
        await using var verify = fixture.CreateContext();
        Assert.True(await verify.ExamStudents.AnyAsync(x => x.StudentId == user.Id));
    }

    [Fact]
    public async Task RosterReadinessCountsStayInSqlAndExistingScheduleIsPreserved()
    {
        var d = await SeedAsync();
        await using (var db = fixture.CreateContext())
        {
            var existing = await db.ExamStudents.SingleAsync(x => x.Id == d.Enrollment.Id);
            existing.Schedule(d.Exam.StartAt);
            await db.SaveChangesAsync();
        }
        var user = new User(Guid.NewGuid() + "@example.test", "unused", "Unscheduled", UserStatus.Active);
        await using (var db = fixture.CreateContext()) { db.Add(user); await db.SaveChangesAsync(); }
        await using var provider = Services(d.Lecturer.Id);
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IExamRosterService>();
        var foundation = scope.ServiceProvider.GetRequiredService<ExamFoundationService>();
        await service.AddAsync(d.Exam.Id, user.Email, default);
        var counts = await foundation.CountsAsync(d.Exam.Id, default);
        Assert.Equal(2, counts.Students);
        Assert.Equal(1, counts.ScheduledStudents);
        var roster = await service.ListAsync(d.Exam.Id, default);
        Assert.Equal(d.Exam.StartAt, roster.Single(x => x.EnrollmentId == d.Enrollment.Id).ScheduledAt);
        var added = roster.Single(x => x.StudentId == user.Id);
        Assert.Null(added.ScheduledAt);
        await service.RemoveAsync(d.Exam.Id, added.EnrollmentId, default);
        Assert.Equal(1, (await foundation.CountsAsync(d.Exam.Id, default)).Students);
        Assert.Equal(d.Exam.StartAt, (await service.ListAsync(d.Exam.Id, default)).Single().ScheduledAt);
    }

    [Fact]
    public async Task AdminCanManageAnotherLecturersRosterAndRevokedLecturerCannot()
    {
        var d = await SeedAsync();
        var admin = new User(Guid.NewGuid() + "@example.test", "unused", "Admin", UserStatus.Active);
        admin.AssignRole(UserRole.ADMIN);
        await using (var db = fixture.CreateContext()) { db.Add(admin); await db.SaveChangesAsync(); }
        await using (var provider = Services(admin.Id, lecturer: false, admin: true))
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IExamRosterService>();
            Assert.Single(await service.ListAsync(d.Exam.Id, default));
            await service.RemoveAsync(d.Exam.Id, d.Enrollment.Id, default);
            Assert.Empty(await service.ListAsync(d.Exam.Id, default));
            await service.AddAsync(d.Exam.Id, d.Student.Email, default);
        }
        await using (var db = fixture.CreateContext())
            await db.LecturerSubjects.Where(x => x.LecturerId == d.Lecturer.Id).ExecuteDeleteAsync();
        await using var lecturerProvider = Services(d.Lecturer.Id);
        await using var lecturerScope = lecturerProvider.CreateAsyncScope();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => lecturerScope.ServiceProvider.GetRequiredService<IExamRosterService>()
            .ListAsync(d.Exam.Id, default));
    }
}
