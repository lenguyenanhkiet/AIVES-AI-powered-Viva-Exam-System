using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
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

    // Fakes for ExamService testing
    private sealed class FakeExamRepository : IExamRepository
    {
        public readonly Dictionary<Guid, Exam> Store = new();

        public Task<Exam?> FindByIdAsync(Guid examId, CancellationToken ct) =>
            Task.FromResult(Store.TryGetValue(examId, out var exam) ? exam : null);

        public Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExamSummary>>(
                Store.Values.Select(x => new ExamSummary(x.Id, x.SubjectId, x.Name, x.Status, x.StartAt, x.EndAt)).ToList());

        public void Add(Exam exam) => Store[exam.Id] = exam;
        public void Remove(Exam exam) => Store.Remove(exam.Id);
    }

    private sealed class FakeExamFoundationReader : IExamFoundationReader
    {
        public int PoolQuestions { get; set; } = 10;
        public int Students { get; set; } = 5;
        public int ScheduledStudents { get; set; } = 5;

        public Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExamSummary>>([]);

        public Task<ExamFoundationCounts> CountsAsync(Guid examId, CancellationToken ct) =>
            Task.FromResult(new ExamFoundationCounts(PoolQuestions, Students, ScheduledStudents, 0));
    }

    private sealed class FakeExamAccessStore : IExamAccessStore
    {
        public Task<ExamAccess?> FindExamAsync(Guid examId, CancellationToken ct) =>
            Task.FromResult<ExamAccess?>(new ExamAccess(examId, SubjectId, LecturerId));

        public Task<ExamStudentAccess?> FindEnrollmentAsync(Guid enrollmentId, CancellationToken ct) =>
            Task.FromResult<ExamStudentAccess?>(null);

        public Task<bool> IsAssignedAsync(Guid lecturerId, Guid subjectId, CancellationToken ct) =>
            Task.FromResult(true);

        public Task<bool> IsEnrolledAsync(Guid examId, Guid studentId, CancellationToken ct) =>
            Task.FromResult(true);
    }

    private sealed class FakeUnitOfWork : IExamUnitOfWork
    {
        public bool Executed { get; private set; }
        public IReadOnlyCollection<Guid> ChangedIds { get; private set; } = [];

        public async Task ExecuteAsync(Func<CancellationToken, Task> work, IReadOnlyCollection<Guid> changedExamIds, CancellationToken ct)
        {
            Executed = true;
            ChangedIds = changedExamIds;
            await work(ct);
        }
    }

    private sealed record TestActor(Guid Id, bool IsAdministrator = false, bool IsLecturer = true) : ICurrentUser;

    [Fact]
    public async Task Service_Publish_rejects_when_readiness_conditions_fail()
    {
        var repo = new FakeExamRepository();
        var reader = new FakeExamFoundationReader();
        var accessStore = new FakeExamAccessStore();
        var actor = new TestActor(LecturerId);
        var guard = new ExamAccessGuard(accessStore, actor);
        var uow = new FakeUnitOfWork();

        var exam = CreateValidDraftExam();
        repo.Store[exam.Id] = exam;
        var service = new ExamService(repo, reader, guard, actor, uow);

        // 1. Question pool < NumberOfMainQuestions (3 required, 2 present)
        reader.PoolQuestions = 2;
        var ex1 = await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.TransitionAsync(exam.Id, ExamStatus.Published, exam.RowVersion, default));
        Assert.Contains("Question pool must contain at least 3 questions", ex1.Message);

        // 2. Students count == 0
        reader.PoolQuestions = 10;
        reader.Students = 0;
        var ex2 = await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.TransitionAsync(exam.Id, ExamStatus.Published, exam.RowVersion, default));
        Assert.Contains("At least one student must be enrolled", ex2.Message);

        // 3. Scheduled students < Students count (3 scheduled / 5 enrolled)
        reader.Students = 5;
        reader.ScheduledStudents = 3;
        var ex3 = await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.TransitionAsync(exam.Id, ExamStatus.Published, exam.RowVersion, default));
        Assert.Contains("All students must be scheduled", ex3.Message);

        // 4. All ready -> successfully publishes
        reader.ScheduledStudents = 5;
        await service.TransitionAsync(exam.Id, ExamStatus.Published, exam.RowVersion, default);
        Assert.Equal(ExamStatus.Published, exam.Status);
        Assert.True(uow.Executed);
        Assert.Contains(exam.Id, uow.ChangedIds);
    }

    [Fact]
    public async Task Service_Delete_rejects_non_draft_exam()
    {
        var repo = new FakeExamRepository();
        var reader = new FakeExamFoundationReader();
        var accessStore = new FakeExamAccessStore();
        var actor = new TestActor(LecturerId);
        var guard = new ExamAccessGuard(accessStore, actor);
        var uow = new FakeUnitOfWork();

        var exam = CreateValidDraftExam();
        exam.Publish();
        repo.Store[exam.Id] = exam;

        var service = new ExamService(repo, reader, guard, actor, uow);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.DeleteDraftAsync(exam.Id, exam.RowVersion, default));
        Assert.True(repo.Store.ContainsKey(exam.Id));
    }

    [Fact]
    public async Task Service_Delete_removes_draft_exam()
    {
        var repo = new FakeExamRepository();
        var reader = new FakeExamFoundationReader();
        var accessStore = new FakeExamAccessStore();
        var actor = new TestActor(LecturerId);
        var guard = new ExamAccessGuard(accessStore, actor);
        var uow = new FakeUnitOfWork();

        var exam = CreateValidDraftExam();
        repo.Store[exam.Id] = exam;

        var service = new ExamService(repo, reader, guard, actor, uow);

        await service.DeleteDraftAsync(exam.Id, exam.RowVersion, default);
        Assert.False(repo.Store.ContainsKey(exam.Id));
        Assert.True(uow.Executed);
    }

    [Fact]
    public async Task Service_Update_rejects_subject_change()
    {
        var repo = new FakeExamRepository();
        var reader = new FakeExamFoundationReader();
        var accessStore = new FakeExamAccessStore();
        var actor = new TestActor(LecturerId);
        var guard = new ExamAccessGuard(accessStore, actor);
        var uow = new FakeUnitOfWork();

        var exam = CreateValidDraftExam();
        repo.Store[exam.Id] = exam;

        var service = new ExamService(repo, reader, guard, actor, uow);
        var diffSubjectId = Guid.NewGuid();
        var req = new ExamConfiguration(diffSubjectId, exam.Name, exam.StartAt, exam.EndAt,
            exam.DurationMinutes, exam.NumberOfMainQuestions, exam.MaxFollowUpPerQuestion);

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            service.UpdateAsync(exam.Id, req, exam.RowVersion, default));
        Assert.Contains("Subject cannot be changed", ex.Message);
    }
}
