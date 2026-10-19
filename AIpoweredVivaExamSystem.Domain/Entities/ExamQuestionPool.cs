using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public sealed class ExamQuestionPool : BaseEntity
{
    public Guid ExamId { get; private set; }
    public Guid QuestionId { get; private set; }
    private ExamQuestionPool() { }
    public ExamQuestionPool(Guid examId, Guid questionId)
    {
        DomainRules.RequiredId(examId, nameof(ExamId)); DomainRules.RequiredId(questionId, nameof(QuestionId));
        ExamId = examId; QuestionId = questionId;
    }
}
