using AIpoweredVivaExamSystem.Domain.Common;
using System.Collections.Generic;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public class Role : BaseEntity, IAggregateRoot
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();

    // Static constants for basic roles
    public const string Admin = "ADMIN";
    public const string Lecturer = "LECTURER";
    public const string Student = "STUDENT";

    private Role() { } // Private constructor for EF Core

    public Role(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public void Update(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
