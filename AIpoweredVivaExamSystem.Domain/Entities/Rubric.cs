using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public class Rubric : AuditableEntity, IAggregateRoot
{
    private readonly List<RubricCriterion> _criteria = [];

    public Guid QuestionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal TotalScore { get; private set; }
    public IReadOnlyCollection<RubricCriterion> Criteria => _criteria.AsReadOnly();

    private Rubric() { }

    public Rubric(Guid questionId, string name, string? description,
        IReadOnlyCollection<CriterionDefinition> criteria)
    {
        DomainRules.RequiredId(questionId, nameof(QuestionId));
        QuestionId = questionId;
        Update(name, description, criteria);
    }

    // Validates the entire replacement before changing the aggregate.
    public void Update(string name, string? description, IReadOnlyCollection<CriterionDefinition> criteria)
    {
        var normalizedName = DomainRules.RequiredText(name, nameof(Name), 255);
        if (criteria is null || criteria.Count == 0)
            throw new DomainValidationException("A rubric must contain at least one criterion.");
        foreach (var definition in criteria)
        {
            if (definition is null)
                throw new DomainValidationException("A criterion must not be null.");
            RubricCriterion.Validate(definition);
        }
        var ids = criteria.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToArray();
        if (ids.Distinct().Count() != ids.Length || ids.Any(id => _criteria.All(x => x.Id != id)))
            throw new DomainValidationException("Criterion IDs must be distinct and belong to this rubric.");
        if (criteria.Select(x => x.DisplayOrder).Distinct().Count() != criteria.Count)
            throw new DomainValidationException("DisplayOrder must be unique within a rubric.");
        var total = criteria.Sum(x => x.MaxScore);
        if (total > 999.99m)
            throw new DomainValidationException("TotalScore must not exceed 999.99.");

        _criteria.RemoveAll(x => !ids.Contains(x.Id));
        foreach (var definition in criteria)
        {
            if (definition.Id is Guid id)
                _criteria.Single(x => x.Id == id).Update(definition);
            else
                _criteria.Add(new RubricCriterion(Id, definition));
        }
        Name = normalizedName;
        Description = description;
        TotalScore = total;
    }
}
