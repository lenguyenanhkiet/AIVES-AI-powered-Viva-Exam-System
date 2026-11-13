using AIpoweredVivaExamSystem.Application.Exams;
using ContractExamStatus = AIpoweredVivaExamSystem.Application.Contracts.ExamStatus;
using ContractEnums = AIpoweredVivaExamSystem.Application.Contracts;
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

        var availableRows = await ExamPoolQueries.EligibleQuestionBank(db, exam).AsNoTracking()
            .Where(q => !db.ExamQuestionPools.IgnoreQueryFilters().Any(p => p.ExamId == examId && p.QuestionId == q.Id))
            .OrderBy(q => q.Content)
            .Select(q => new { q.Id, q.Content, q.BloomLevel, q.Difficulty, q.Status, IsDeleted = q.DeletedAt != null })
            .ToArrayAsync(ct);
        var available = availableRows.Select(q => new ExamPoolQuestion(q.Id, q.Content,
            (ContractEnums.BloomLevel)q.BloomLevel, (ContractEnums.QuestionDifficulty)q.Difficulty,
            (ContractEnums.QuestionStatus)q.Status, true, q.IsDeleted, true)).ToArray();

        var eligiblePoolIds = (await ExamPoolQueries.EligiblePooledQuestions(db, exam)
            .Select(q => q.Id).ToArrayAsync(ct)).ToHashSet();

        var pooledRows = await (from pool in db.ExamQuestionPools.IgnoreQueryFilters().AsNoTracking()
                            join question in db.Questions.IgnoreQueryFilters().AsNoTracking() on pool.QuestionId equals question.Id
                            where pool.ExamId == examId
                            orderby question.Content
                            select new { question.Id, question.Content, question.BloomLevel,
                                question.Difficulty, question.Status,
                                HasRubric = db.Rubrics.IgnoreQueryFilters().Any(r => r.QuestionId == question.Id && r.DeletedAt == null),
                                IsDeleted = question.DeletedAt != null }).ToArrayAsync(ct);
        var pooled = pooledRows.Select(q => new ExamPoolQuestion(q.Id, q.Content,
            (ContractEnums.BloomLevel)q.BloomLevel, (ContractEnums.QuestionDifficulty)q.Difficulty,
            (ContractEnums.QuestionStatus)q.Status, q.HasRubric, q.IsDeleted,
            eligiblePoolIds.Contains(q.Id))).ToArray();

        var summary = new ExamSummary(exam.Id, exam.SubjectId, exam.Name, (ContractExamStatus)exam.Status,
            exam.StartAt, exam.EndAt);
        return new ExamPoolPage(summary, exam.NumberOfMainQuestions, pooled.Length, eligiblePoolIds.Count,
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
