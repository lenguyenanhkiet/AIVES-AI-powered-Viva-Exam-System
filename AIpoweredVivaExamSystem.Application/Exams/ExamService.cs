using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;

namespace AIpoweredVivaExamSystem.Application.Exams;

public sealed class ExamService(
    IExamRepository repository,
    IExamFoundationReader reader,
    ExamAccessGuard guard,
    ICurrentUser actor,
    IExamUnitOfWork uow) : IExamService
{
    public async Task<ExamDetails> CreateAsync(ExamConfiguration request, CancellationToken ct)
    {
        await guard.RequireSubjectAsync(request.SubjectId, ct);

        var exam = new Exam(
            request.SubjectId,
            actor.Id,
            request.Name,
            request.StartAt,
            request.EndAt,
            request.DurationMinutes,
            request.NumberOfMainQuestions,
            request.MaxFollowUpPerQuestion);

        await uow.ExecuteAsync(async _ =>
        {
            repository.Add(exam);
            await Task.CompletedTask;
        }, [exam.Id], ct);

        return Map(exam);
    }

    public async Task<ExamDetails> GetAsync(Guid examId, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        var exam = await repository.FindByIdAsync(examId, ct)
            ?? throw new ResourceNotFoundException("Exam not found.");

        return Map(exam);
    }

    public async Task<IReadOnlyList<ExamSummary>> ListManagedAsync(CancellationToken ct)
    {
        guard.RequireManager();
        return await repository.ListManagedAsync(ct);
    }

    public async Task UpdateAsync(Guid examId, ExamConfiguration request, byte[] expectedVersion, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        await guard.RequireSubjectAsync(request.SubjectId, ct);

        var exam = await repository.FindByIdAsync(examId, ct)
            ?? throw new ResourceNotFoundException("Exam not found.");

        if (request.SubjectId != exam.SubjectId)
        {
            throw new DomainValidationException("Subject cannot be changed after exam creation.");
        }

        ValidateVersion(exam, expectedVersion);

        exam.Update(
            request.Name,
            request.StartAt,
            request.EndAt,
            request.DurationMinutes,
            request.NumberOfMainQuestions,
            request.MaxFollowUpPerQuestion);

        await uow.ExecuteAsync(async _ => { await Task.CompletedTask; }, [exam.Id], ct);
    }

    public async Task TransitionAsync(Guid examId, ExamStatus target, byte[] expectedVersion, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        var exam = await repository.FindByIdAsync(examId, ct)
            ?? throw new ResourceNotFoundException("Exam not found.");

        ValidateVersion(exam, expectedVersion);

        switch (target)
        {
            case ExamStatus.Published:
                await ValidatePublishReadinessAsync(examId, exam, ct);
                exam.Publish();
                break;

            case ExamStatus.Ongoing:
                exam.Start(DateTimeOffset.UtcNow);
                break;

            case ExamStatus.Completed:
                exam.Complete();
                break;

            case ExamStatus.Cancelled:
                exam.Cancel();
                break;

            default:
                throw new DomainValidationException($"Invalid target status transition: {target}");
        }

        await uow.ExecuteAsync(async _ => { await Task.CompletedTask; }, [exam.Id], ct);
    }

    public async Task DeleteDraftAsync(Guid examId, byte[] expectedVersion, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        var exam = await repository.FindByIdAsync(examId, ct)
            ?? throw new ResourceNotFoundException("Exam not found.");

        if (exam.Status != ExamStatus.Draft)
            throw new DomainValidationException("Only draft exams can be deleted.");

        ValidateVersion(exam, expectedVersion);

        await uow.ExecuteAsync(async _ =>
        {
            repository.Remove(exam);
            await Task.CompletedTask;
        }, [exam.Id], ct);
    }

    private async Task ValidatePublishReadinessAsync(Guid examId, Exam exam, CancellationToken ct)
    {
        var counts = await reader.CountsAsync(examId, ct);

        if (counts.PoolQuestions < exam.NumberOfMainQuestions)
        {
            throw new DomainValidationException(
                $"Cannot publish exam: Question pool must contain at least {exam.NumberOfMainQuestions} questions (currently {counts.PoolQuestions}).");
        }

        if (counts.Students <= 0)
        {
            throw new DomainValidationException("Cannot publish exam: At least one student must be enrolled.");
        }

        if (counts.ScheduledStudents < counts.Students)
        {
            throw new DomainValidationException(
                $"Cannot publish exam: All students must be scheduled ({counts.ScheduledStudents}/{counts.Students} scheduled).");
        }
    }

    private static void ValidateVersion(Exam exam, byte[]? expectedVersion)
    {
        if (expectedVersion is { Length: > 0 } && !expectedVersion.SequenceEqual(exam.RowVersion))
        {
            throw new ResourceConflictException("Exam data changed. Reload and try again.");
        }
    }

    private static ExamDetails Map(Exam exam) => new(
        new ExamSummary(exam.Id, exam.SubjectId, exam.Name, exam.Status, exam.StartAt, exam.EndAt),
        exam.DurationMinutes,
        exam.NumberOfMainQuestions,
        exam.MaxFollowUpPerQuestion,
        exam.RowVersion);
}
