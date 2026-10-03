using AIpoweredVivaExamSystem.Application.Common.Interfaces;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AIpoweredVivaExamSystem.Application.Features.Auth.Commands.RegisterUser;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterUserCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Check if email already exists
        var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailExists)
        {
            throw new InvalidOperationException("Email already exists.");
        }

        // Hash the password
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // Create the user
        var user = new User(
            request.Email,
            request.PhoneNumber,
            passwordHash,
            request.FullName,
            UserStatus.Active
        );

        // Optional: Assign default role (e.g., STUDENT)
        var studentRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == Role.Student, cancellationToken);
        if (studentRole != null)
        {
            user.UserRoles.Add(new UserRole(user.Id, studentRole.Id));
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
