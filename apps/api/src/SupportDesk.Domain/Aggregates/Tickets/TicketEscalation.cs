using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Tickets;

public sealed class TicketEscalation : Entity
{
    private TicketEscalation() { }

    internal TicketEscalation(Ticket ticket, TriageDecision decision, string reason, string actor, DateTime now)
    {
        TicketId = ticket.Id;
        FromPriority = ticket.Priority;
        ToPriority = decision.Priority;
        FromAgentId = ticket.AssignedAgentId;
        ToAgentId = decision.AssignedAgentId;
        FromDueAtUtc = ticket.DueAtUtc;
        ToDueAtUtc = now.Add(decision.Window);
        Reason = reason;
        EscalatedBy = actor;
        EscalatedAtUtc = now;
    }

    public int TicketId { get; private set; }
    public TicketPriority FromPriority { get; private set; }
    public TicketPriority ToPriority { get; private set; }
    public int? FromAgentId { get; private set; }
    public int? ToAgentId { get; private set; }
    public DateTime? FromDueAtUtc { get; private set; }
    public DateTime? ToDueAtUtc { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string EscalatedBy { get; private set; } = string.Empty;
    public DateTime EscalatedAtUtc { get; private set; }
}
