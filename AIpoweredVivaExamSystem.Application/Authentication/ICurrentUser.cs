namespace AIpoweredVivaExamSystem.Application.Authentication;

public interface ICurrentUser
{
    Guid Id { get; }
    bool IsAdministrator { get; }
    bool IsLecturer { get; }
}
