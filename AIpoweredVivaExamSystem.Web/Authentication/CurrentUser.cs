using System.Security.Claims;
using AIpoweredVivaExamSystem.Application.Authentication;

namespace AIpoweredVivaExamSystem.Web.Authentication;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : Guid.Empty;
    public bool IsAdministrator => accessor.HttpContext?.User.IsInRole("ADMIN") == true;
    public bool IsLecturer => accessor.HttpContext?.User.IsInRole("LECTURER") == true;
}
