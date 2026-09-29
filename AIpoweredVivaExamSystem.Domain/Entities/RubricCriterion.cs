using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public class RubricCriterion : AuditableEntity
{
    public Guid RubricId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ExpectedConcepts { get; private set; }
    public decimal MaxScore { get; private set; }
    public int DisplayOrder { get; private set; }

    private RubricCriterion() { }

    internal RubricCriterion(Guid rubricId, CriterionDefinition definition)
    {
        RubricId = rubricId;
        Update(definition);
    }

    internal static void Validate(CriterionDefinition definition)
    {
        DomainRules.RequiredText(definition.Name, nameof(Name), 255);
        if (definition.MaxScore <= 0 || definition.MaxScore > 999.99m
            || decimal.Round(definition.MaxScore, 2) != definition.MaxScore)
            throw new DomainValidationException("MaxScore must be between 0.01 and 999.99 with at most two decimal places.");
        if (definition.DisplayOrder < 1)
            throw new DomainValidationException("DisplayOrder must be at least 1.");
        if (definition.Id == Guid.Empty)
            throw new DomainValidationException("Criterion Id must not be empty.");
    }

    internal void Update(CriterionDefinition definition)
    {
        Validate(definition);
        Name = definition.Name.Trim();
        Description = definition.Description;
        ExpectedConcepts = definition.ExpectedConcepts;
        MaxScore = definition.MaxScore;
        DisplayOrder = definition.DisplayOrder;
    }
}
