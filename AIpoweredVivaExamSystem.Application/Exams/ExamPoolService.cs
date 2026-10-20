using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.Exams;

public sealed class ExamPoolService(IExamPoolStore store, ExamAccessGuard guard, IExamUnitOfWork unitOfWork) : IExamPoolReader
{
    public async Task<ExamPoolPage> GetPageAsync(Guid examId, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        return await store.ReadAsync(examId, ct) ?? throw new ResourceNotFoundException("Exam not found.");
    }

    public async Task<int> CountEligiblePoolQuestionsAsync(Guid examId, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        return await store.CountEligiblePoolQuestionsAsync(examId, ct);
    }

    public async Task AddAsync(Guid examId, IReadOnlyCollection<Guid> questionIds, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        var ids = NormalizeIds(questionIds);
        var changedExamIds = new List<Guid>();

        await unitOfWork.ExecuteAsync(async token =>
        {
            var exam = await store.LockExamAsync(examId, token);
            await guard.CanManageExamAsync(examId, token);
            RequireDraft(exam);

            // Validate the entire batch before staging any mapping, so a single invalid ID
            // cannot leave a partially updated pool.
            var eligibleIds = await store.FindEligibleQuestionIdsAsync(exam, ids, token);
            if (eligibleIds.Count != ids.Length)
                throw new DomainValidationException("Mỗi câu được chọn phải thuộc đúng môn, đã duyệt, còn rubric và do giảng viên phụ trách sở hữu.");

            var existingIds = await store.FindPooledQuestionIdsAsync(examId, ids, token);
            foreach (var questionId in ids)
                if (!existingIds.Contains(questionId))
                    store.Add(new ExamQuestionPool(examId, questionId));

            if (ids.Any(id => !existingIds.Contains(id)))
            {
                exam.UpdatedAt = DateTimeOffset.UtcNow;
                changedExamIds.Add(examId);
            }
        }, changedExamIds, ct);
    }

    public async Task RemoveAsync(Guid examId, IReadOnlyCollection<Guid> questionIds, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        var ids = NormalizeIds(questionIds);
        var changedExamIds = new List<Guid>();

        await unitOfWork.ExecuteAsync(async token =>
        {
            var exam = await store.LockExamAsync(examId, token);
            await guard.CanManageExamAsync(examId, token);
            RequireDraft(exam);

            var entries = await store.FindPoolEntriesAsync(examId, ids, token);
            if (entries.Length > 0)
            {
                store.RemoveRange(entries);
                exam.UpdatedAt = DateTimeOffset.UtcNow;
                changedExamIds.Add(examId);
            }
        }, changedExamIds, ct);
    }

    private static Guid[] NormalizeIds(IReadOnlyCollection<Guid> questionIds)
    {
        if (questionIds is null || questionIds.Count == 0)
            throw new DomainValidationException("Hãy chọn ít nhất một câu hỏi.");
        var ids = questionIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
            throw new DomainValidationException("Hãy chọn ít nhất một câu hỏi hợp lệ.");
        return ids;
    }

    private static void RequireDraft(Exam exam)
    {
        if (exam.Status != ExamStatus.Draft)
            throw new ResourceConflictException("Chỉ thay đổi pool câu hỏi khi kỳ thi ở trạng thái Draft.");
    }
}
