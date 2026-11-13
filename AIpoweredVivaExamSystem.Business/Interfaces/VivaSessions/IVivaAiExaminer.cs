using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.Business.VivaSessions;

public interface IVivaAiExaminer
{
    Task<ExaminerDecision> EvaluateAndFollowUpAsync(ExaminerEvaluationContext context, CancellationToken ct);
}
