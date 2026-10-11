using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Application.Abstractions;

public interface IPasswordService
{
    string Hash(Agent agent, string password);

    bool Verify(Agent agent, string password);
}
