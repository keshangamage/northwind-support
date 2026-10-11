using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Login;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public Task<LoginResponse> Login(
        LoginRequest request, [FromServices] LoginCommandHandler handler, CancellationToken ct) =>
        handler.HandleAsync(request, ct);

    [HttpGet("me")]
    public async Task<ActionResult<AuthUserDto>> Me([FromServices] IAgentRepository agents, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            return Unauthorized();
        }

        var agent = await agents.GetByIdAsync(id, ct);
        if (agent is null || !agent.IsActive)
        {
            return Unauthorized();
        }

        return new AuthUserDto(agent.Id, agent.FullName, agent.Email);
    }
}
