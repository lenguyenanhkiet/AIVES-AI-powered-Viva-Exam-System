using AIpoweredVivaExamSystem.Application.Common.Interfaces;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AIpoweredVivaExamSystem.Application.Features.Auth.Commands.RegisterUser;

public class EmailAlreadyExistsException : Exception
{
    public EmailAlreadyExistsException(string email)
        : base($"Email '{email}' is already registered.") { }
}

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public RegisterUserCommandHandler(IApplicationDbContext context, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Normalize email: Trim + ToLowerInvariant
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Check email uniqueness against the normalized email
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailExists)
        {
            throw new EmailAlreadyExistsException(normalizedEmail);
        }

        // Create user first (needed to pass to PasswordHasher)
        // Password is NOT trimmed or lowercased - used as-is
        var user = new User(
            normalizedEmail,
            request.PhoneNumber,
            string.Empty, // temporary, will be set below
            request.FullName,
            UserStatus.Active
        );

        // Hash password using ASP.NET Core Identity's PasswordHasher<User>
        // The hasher manages salt internally - no manual salt needed
        var passwordHash = _passwordHasher.HashPassword(user, request.Password);
        user.ChangePassword(passwordHash);

        // Public registration always assigns STUDENT role - client cannot choose ADMIN or LECTURER
        var studentRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == Role.Student, cancellationToken);
        if (studentRole != null)
        {
            user.UserRoles.Add(new UserRole(user.Id, studentRole.Id));
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}

