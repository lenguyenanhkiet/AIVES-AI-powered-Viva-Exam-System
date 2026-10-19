using System.Security.Cryptography;
using System.Text.Json;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class VivaSessionStore(ApplicationDbContext db, IExamUnitOfWork uow, ICurrentUser actor) : IVivaSessionStore
{
    public async Task<Guid> FindEnrollmentAsync(Guid sessionId, CancellationToken ct) =>
        await db.VivaSessions.AsNoTracking().Where(s => s.Id == sessionId)
            .Select(s => (Guid?)s.ExamStudentId).SingleOrDefaultAsync(ct)
        ?? throw new ResourceNotFoundException("Session not found.");

    private async Task<ExamStudent> OwnAsync(Guid id, CancellationToken ct) =>
        await db.ExamStudents.SingleOrDefaultAsync(r => r.Id == id && r.StudentId == actor.Id, ct)
        ?? throw new ResourceNotFoundException("Enrollment not found.");

    // The exam row is locked first for every mutation. This serializes selection across students,
    // protects the previous-session preference and cooperates with lifecycle/schedule updates.
    private Task<Exam> LockExamAsync(Guid id, CancellationToken ct) => db.Exams
        .FromSqlInterpolated($"SELECT * FROM Exams WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
        .IgnoreQueryFilters().SingleAsync(e => e.DeletedAt == null, ct);

    private async Task<ExamStudent> LockEnrollmentAsync(Guid id, CancellationToken ct)
    {
        var entry = await db.ExamStudents
            .FromSqlInterpolated($"SELECT * FROM ExamStudents WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
            .SingleAsync(r => r.StudentId == actor.Id, ct);
        await db.Entry(entry).ReloadAsync(ct);
        return entry;
    }

    public async Task<StudentSession> GetOrCreateAsync(Guid enrollmentId, CancellationToken ct)
    {
        var examId = (await OwnAsync(enrollmentId, ct)).ExamId;
        Guid sessionId = Guid.Empty;
        // Notify only if a session was created, after the transaction commits.
        var changed = new List<Guid>();
        await uow.ExecuteAsync(async token =>
        {
            var exam = await LockExamAsync(examId, token);
            var entry = await LockEnrollmentAsync(enrollmentId, token);
            var existing = await db.VivaSessions.SingleOrDefaultAsync(s => s.ExamStudentId == enrollmentId, token);
            if (existing is not null) { sessionId = existing.Id; return; }
            var now = DateTimeOffset.UtcNow;
            if (exam.Status != ExamStatus.Ongoing || entry.ScheduledAt is not { } start ||
                start < exam.StartAt || start.AddMinutes(exam.DurationMinutes) > exam.EndAt ||
                now < start || now >= start.AddMinutes(exam.DurationMinutes) || now >= exam.EndAt)
                throw new DomainValidationException("The exam must be ongoing and your scheduled slot must be open.");

            var assigned = await db.LecturerSubjects.AnyAsync(a => a.LecturerId == exam.LecturerId && a.SubjectId == exam.SubjectId, token);
            if (!assigned) throw new DomainValidationException("The exam lecturer is no longer assigned to this subject.");
            var pool = db.ExamQuestionPools.IgnoreQueryFilters().Where(p => p.ExamId == exam.Id).Select(p => p.QuestionId);
            var questions = await db.Questions.FromSqlRaw("SELECT * FROM Questions WITH (HOLDLOCK)").IgnoreQueryFilters()
                .Where(q => pool.Contains(q.Id) && q.SubjectId == exam.SubjectId && q.DeletedAt == null &&
                    q.Status == QuestionStatus.Approved && q.CreatedBy == exam.LecturerId).ToArrayAsync(token);
            var ids = questions.Select(q => q.Id).ToArray();
            var rubrics = await db.Rubrics.FromSqlRaw("SELECT * FROM Rubrics WITH (HOLDLOCK)").IgnoreQueryFilters().Include(r => r.Criteria)
                .Where(r => ids.Contains(r.QuestionId) && r.DeletedAt == null).ToArrayAsync(token);
            var eligible = questions.Where(q => rubrics.Any(r => r.QuestionId == q.Id)).ToArray();
            if (eligible.Length < exam.NumberOfMainQuestions)
                throw new DomainValidationException("There are not enough approved questions with rubrics in the exam pool.");
            var previous = await db.VivaSessions.IgnoreQueryFilters()
                .Where(s => db.ExamStudents.IgnoreQueryFilters().Any(r => r.Id == s.ExamStudentId && r.ExamId == examId))
                .OrderByDescending(s => s.StartedAt).ThenByDescending(s => s.Id).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(token);
            var previousIds = await db.SessionQuestions.IgnoreQueryFilters().Where(q => q.VivaSessionId == previous)
                .Select(q => q.QuestionId).ToArrayAsync(token);
            var selected = Select(eligible.Select(q => q.Id).ToArray(), previousIds, exam.NumberOfMainQuestions);
            var session = new VivaSession(enrollmentId, now);
            db.VivaSessions.Add(session);
            for (var i = 0; i < selected.Length; i++)
            {
                var q = eligible.Single(q => q.Id == selected[i]);
                var rubric = rubrics.Single(r => r.QuestionId == q.Id);
                var snapshot = JsonSerializer.Serialize(new { rubric.Name, rubric.Description, rubric.TotalScore,
                    Criteria = rubric.Criteria.OrderBy(c => c.DisplayOrder), q.ExpectedAnswer });
                db.SessionQuestions.Add(new SessionQuestion(session.Id, q.Id, i + 1, q.Content, snapshot));
            }
            sessionId = session.Id;
            changed.Add(examId);
        }, changed, ct);
        var saved = await db.VivaSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId, ct);
        var safeQuestions = await db.SessionQuestions.IgnoreQueryFilters().AsNoTracking()
            .Where(q => q.VivaSessionId == sessionId)
            .OrderBy(q => q.Position).Select(q => new StudentQuestion(q.QuestionId, q.Position, q.ContentSnapshot)).ToArrayAsync(ct);
        return new StudentSession(saved.Id, saved.StartedAt, saved.CompletedAt, safeQuestions);
    }

    public static Guid[] Select(Guid[] eligible, Guid[] previous, int count)
    {
        if (count <= 0 || eligible.Distinct().Count() < count) throw new DomainValidationException("Insufficient question pool.");
        var fresh = eligible.Distinct().Where(id => !previous.Contains(id)).ToArray();
        var used = eligible.Distinct().Where(previous.Contains).ToArray();
        RandomNumberGenerator.Shuffle(fresh.AsSpan());
        RandomNumberGenerator.Shuffle(used.AsSpan());
        return fresh.Concat(used).Take(count).ToArray();
    }

    public async Task CompleteAsync(Guid sessionId, CancellationToken ct)
    {
        var enrollmentId = await FindEnrollmentAsync(sessionId, ct);
        var examId = (await OwnAsync(enrollmentId, ct)).ExamId;
        var changed = new List<Guid>();
        await uow.ExecuteAsync(async token =>
        {
            await LockExamAsync(examId, token);
            await LockEnrollmentAsync(enrollmentId, token);
            var session = await db.VivaSessions.SingleAsync(s => s.Id == sessionId, token);
            if (session.CompletedAt.HasValue) return;
            session.Complete(DateTimeOffset.UtcNow);
            changed.Add(examId);
        }, changed, ct);
    }
}
