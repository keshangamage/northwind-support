using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Presentation.Auth;

public sealed class JwtTokenIssuer(JwtOptions options, IClock clock) : ITokenIssuer
{
    public LoginResponse Issue(Agent agent)
    {
        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(options.LifetimeMinutes);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, agent.Id.ToString()),
            new Claim(ClaimTypes.Name, agent.FullName),
            new Claim(ClaimTypes.Email, agent.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(options.Issuer, options.Audience, claims,
            notBefore: now, expires: expiresAtUtc, signingCredentials: credentials);

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc,
            new AuthUserDto(agent.Id, agent.FullName, agent.Email));
    }
}
