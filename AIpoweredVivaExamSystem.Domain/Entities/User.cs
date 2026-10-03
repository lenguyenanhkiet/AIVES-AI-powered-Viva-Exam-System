using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public class User : AuditableEntity, IAggregateRoot
{
    public string Email { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public UserStatus Status { get; private set; }

    public ICollection<UserRole> UserRoles { get; private set; } = new List<UserRole>();
    private User() { } // Private constructor for EF Core

    public User(string email, string phoneNumber, string passwordHash, string fullName, UserStatus status)
    {
        Email = email;
        PhoneNumber = phoneNumber;
        PasswordHash = passwordHash;
        FullName = fullName;
        Status = status;
    }
    public void UpdateProfile(string fullName, string phoneNumber)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
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
