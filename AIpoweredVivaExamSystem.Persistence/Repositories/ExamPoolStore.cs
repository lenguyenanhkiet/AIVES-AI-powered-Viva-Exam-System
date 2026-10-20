using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class ExamPoolStore(ApplicationDbContext db) : IExamPoolStore
{
    public Task<Exam> LockExamAsync(Guid examId, CancellationToken ct) => ExamLock.LockAsync(db, examId, ct);

    public async Task<ExamPoolPage?> ReadAsync(Guid examId, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == examId && e.DeletedAt == null, ct);
        if (exam is null) return null;

        var available = await ExamPoolQueries.EligibleQuestionBank(db, exam).AsNoTracking()
            .Where(q => !db.ExamQuestionPools.IgnoreQueryFilters().Any(p => p.ExamId == examId && p.QuestionId == q.Id))
            .OrderBy(q => q.Content)
            .Select(q => new ExamPoolQuestion(q.Id, q.Content, q.BloomLevel, q.Difficulty, q.Status,
                true, q.DeletedAt != null)).ToArrayAsync(ct);

        var pooled = await (from pool in db.ExamQuestionPools.IgnoreQueryFilters().AsNoTracking()
                            join question in db.Questions.IgnoreQueryFilters().AsNoTracking() on pool.QuestionId equals question.Id
                            where pool.ExamId == examId
                            orderby question.Content
                            select new ExamPoolQuestion(question.Id, question.Content, question.BloomLevel,
                                question.Difficulty, question.Status,
                                db.Rubrics.IgnoreQueryFilters().Any(r => r.QuestionId == question.Id && r.DeletedAt == null),
                                question.DeletedAt != null)).ToArrayAsync(ct);
        var eligiblePoolCount = await ExamPoolQueries.EligiblePooledQuestions(db, exam).CountAsync(ct);

        var summary = new ExamSummary(exam.Id, exam.SubjectId, exam.Name, exam.Status, exam.StartAt, exam.EndAt);
        return new ExamPoolPage(summary, exam.NumberOfMainQuestions, pooled.Length, eligiblePoolCount,
            available, pooled);
    }

    public async Task<int> CountEligiblePoolQuestionsAsync(Guid examId, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == examId && e.DeletedAt == null, ct);
        if (exam is null) return 0;
        return await ExamPoolQueries.EligiblePooledQuestions(db, exam).CountAsync(ct);
    }

    public async Task<IReadOnlySet<Guid>> FindEligibleQuestionIdsAsync(Exam exam,
        IReadOnlyCollection<Guid> questionIds, CancellationToken ct) =>
        (await ExamPoolQueries.EligibleQuestionBank(db, exam).Where(q => questionIds.Contains(q.Id))
            .Select(q => q.Id).ToArrayAsync(ct)).ToHashSet();

    public async Task<IReadOnlySet<Guid>> FindPooledQuestionIdsAsync(Guid examId,
        IReadOnlyCollection<Guid> questionIds, CancellationToken ct) =>
        (await db.ExamQuestionPools.IgnoreQueryFilters().Where(p => p.ExamId == examId && questionIds.Contains(p.QuestionId))
            .Select(p => p.QuestionId).ToArrayAsync(ct)).ToHashSet();

    public Task<ExamQuestionPool[]> FindPoolEntriesAsync(Guid examId,
        IReadOnlyCollection<Guid> questionIds, CancellationToken ct) =>
        db.ExamQuestionPools.Where(p => p.ExamId == examId && questionIds.Contains(p.QuestionId)).ToArrayAsync(ct);

    public void Add(ExamQuestionPool entry) => db.ExamQuestionPools.Add(entry);
    public void RemoveRange(IEnumerable<ExamQuestionPool> entries) => db.ExamQuestionPools.RemoveRange(entries);
}
