namespace AIpoweredVivaExamSystem.Domain.Entities;

// Id is null for a new criterion; existing IDs are preserved when updating a rubric.
public sealed record CriterionDefinition(Guid? Id, string Name, string? Description,
    string? ExpectedConcepts, decimal MaxScore, int DisplayOrder);
