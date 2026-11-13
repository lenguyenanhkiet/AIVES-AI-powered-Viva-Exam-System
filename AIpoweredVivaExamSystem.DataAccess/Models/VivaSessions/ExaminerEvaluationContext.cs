using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;

public sealed record ExaminerEvaluationContext(
    string SubjectName,
    string MainQuestionContent,
    string RubricSnapshotJson,
    int CurrentFollowUpLevel,
    int MaxFollowUpPerQuestion,
    IReadOnlyList<ConversationTurn> History);
