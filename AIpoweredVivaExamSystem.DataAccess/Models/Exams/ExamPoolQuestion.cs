using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Exams;

public sealed record ExamPoolQuestion(Guid Id, string Content, BloomLevel BloomLevel,
    QuestionDifficulty Difficulty, QuestionStatus Status, bool HasRubric, bool IsDeleted, bool IsEligible);
