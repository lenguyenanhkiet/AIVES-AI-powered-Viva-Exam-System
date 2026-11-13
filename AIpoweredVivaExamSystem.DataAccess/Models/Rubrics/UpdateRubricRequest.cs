namespace AIpoweredVivaExamSystem.DataAccess.Models.Rubrics;

public sealed record UpdateRubricRequest(
    string Name, string? Description, List<CriterionRequest> Criteria);
