using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed class GradingValidationException(IReadOnlyDictionary<string,string> errors) : Exception("Kiểm tra các điểm được đánh dấu.")
{ public IReadOnlyDictionary<string,string> Errors { get; } = errors; }
