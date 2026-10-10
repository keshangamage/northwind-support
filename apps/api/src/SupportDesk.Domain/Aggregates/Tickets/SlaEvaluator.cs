using System.Linq.Expressions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    // EF uses this expression in SQL.
    public static Expression<Func<Ticket, TicketSla>> Projection(DateTime now, double atRiskPercent) =>
        ticket => new TicketSla
        {
            Ticket = ticket,
            Status = ticket.DueAtUtc == null ? SlaStatus.NotApplicable
                : ticket.ResolvedAtUtc != null
                    ? ticket.ResolvedAtUtc <= ticket.DueAtUtc ? SlaStatus.Met : SlaStatus.Breached
                : now > ticket.DueAtUtc ? SlaStatus.Breached
                : now >= ticket.DueAtUtc.Value.AddSeconds(
                    -Math.Ceiling((ticket.SlaWindowSeconds ?? 0) * atRiskPercent / 100))
                    ? SlaStatus.AtRisk : SlaStatus.WithinSla
        };
}

public sealed class TicketSla
{
    public required Ticket Ticket { get; init; }
    public SlaStatus Status { get; init; }
}
