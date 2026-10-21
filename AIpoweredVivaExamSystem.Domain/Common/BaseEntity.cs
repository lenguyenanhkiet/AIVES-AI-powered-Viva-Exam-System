namespace AIpoweredVivaExamSystem.Domain.Common;
/// <summary>
/// Base class for all entities in the domain model, providing a unique identifier (Id) for each entity.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}
