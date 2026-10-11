using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IAgentRepository agents, IPasswordService passwords, ITokenIssuer tokens)
{
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (agent is null || !agent.IsActive || string.IsNullOrEmpty(agent.PasswordHash)
            || !passwords.Verify(agent, request.Password))
        {
            throw new UnauthorizedException("Email or password is incorrect.");
        }

        return tokens.Issue(agent);
    }
}
