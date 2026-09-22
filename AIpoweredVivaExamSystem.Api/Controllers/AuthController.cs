using AIpoweredVivaExamSystem.Api.Contracts.Authentication;
using AIpoweredVivaExamSystem.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AIpoweredVivaExamSystem.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(LoginService login) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        // ApiController đã validate DTO; service xác minh mật khẩu và trạng thái tài khoản.
        var user = await login.LoginAsync(request.Email, request.Password, cancellationToken);
        if (user is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");

        // Không để trình duyệt/proxy lưu response đăng nhập vào cache.
        Response.Headers.CacheControl = "no-store";
        return Ok(user);
    }
}
