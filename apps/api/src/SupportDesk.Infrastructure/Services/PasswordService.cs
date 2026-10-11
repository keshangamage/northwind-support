using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Services;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<Agent> _hasher = new();

    public string Hash(Agent agent, string password) => _hasher.HashPassword(agent, password);

    public bool Verify(Agent agent, string password) =>
        _hasher.VerifyHashedPassword(agent, agent.PasswordHash, password) != PasswordVerificationResult.Failed;
}
