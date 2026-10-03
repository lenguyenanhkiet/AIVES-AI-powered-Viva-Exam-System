using MediatR;

namespace AIpoweredVivaExamSystem.Application.Features.Auth.Commands.RegisterUser;

public class RegisterUserCommand : IRequest<bool>
{
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
