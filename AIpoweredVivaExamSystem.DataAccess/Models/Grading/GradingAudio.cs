using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record GradingAudio(Guid ExamId, string FilePath, string MimeType);
