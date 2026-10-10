namespace SupportDesk.Domain.Aggregates.Agents;

public sealed record AgentWorkload(Agent Agent, int OpenTicketCount);
