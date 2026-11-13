namespace AIpoweredVivaExamSystem.DataAccess.Contracts;

// Identity facts used by query filters and audit fields. No HTTP or business dependency.
public interface IDataActor
{
    Guid Id { get; }
    bool IsAdministrator { get; }
    bool IsLecturer { get; }
}
