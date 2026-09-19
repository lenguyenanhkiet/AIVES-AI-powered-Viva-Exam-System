using System;
using System.Collections.Generic;
using System.Text;

namespace AIpoweredVivaExamSystem.Domain.Enums;

public enum UserStatus
{
    Active = 1, // User account is active and can log in
    Inactive = 2, // User account is inactive and cannot log in
    Suspended =3, // User account is temporarily suspended due to policy violations
    Locked = 4, // User account is locked due to multiple failed login attempts
}
