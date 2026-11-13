using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;

public sealed record ConversationTurn(
    AnswerType AnswerType,
    int FollowUpLevel,
    string PromptText,
    string Transcript);
