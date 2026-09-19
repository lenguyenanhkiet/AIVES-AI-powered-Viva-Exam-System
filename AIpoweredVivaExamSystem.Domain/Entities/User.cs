using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public class User : AuditableEntity, IAggregateRoot
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public UserStatus Status { get; private set; }
    private User() { } // Private constructor for EF Core

    public User(string email, string passwordHash, string fullName, UserStatus status)
    {
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        Status = status;
    }
    public void UpdateProfile(string fullName)
    {
        FullName = fullName;
    }

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
    }

    public void Activate()
    {
        Status = UserStatus.Active;
    }

    public void Deactivate()
    {
        Status = UserStatus.Inactive;
    }

    public void Lock()
    {
        Status = UserStatus.Locked;
    }
}
