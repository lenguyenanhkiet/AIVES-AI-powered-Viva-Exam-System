using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed class CriterionGradeInput { public Guid CriterionId { get; set; } public decimal? Score { get; set; } }
