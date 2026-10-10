using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Aggregates.Tickets;

public sealed class TicketTriage(SlaPolicy sla)
{
    public TriageDecision CreateDecision(
        Category category, CustomerTier tier, TicketPriority? requestedPriority,
        IReadOnlyList<AgentWorkload> workloads)
    {
        var priority = category.ForcesCriticalPriority
            ? TicketPriority.Critical
            : requestedPriority ?? TicketPriority.Medium;

        var reason = category.ForcesCriticalPriority
            ? $"{category.Name} requires Critical priority."
            : requestedPriority is null ? "No priority requested; using Medium."
            : $"Using the requested {priority} priority.";

        return Decide(priority, category, tier, workloads, null, reason);
    }

    public TriageDecision EscalationDecision(
        Ticket ticket, Category category, CustomerTier tier, IReadOnlyList<AgentWorkload> workloads)
    {
        ticket.EnsureCanEscalate();
        var priority = ticket.Priority + 1;
        return Decide(priority, category, tier, workloads, ticket.AssignedAgentId,
            $"Raised priority from {ticket.Priority} to {priority}.");
    }

    private TriageDecision Decide(
        TicketPriority priority, Category category, CustomerTier tier,
        IReadOnlyList<AgentWorkload> workloads, int? currentAgentId, string priorityReason)
    {
        var window = sla.GetWindow(priority, tier);
        var owner = TicketAssignment.Select(workloads, category, currentAgentId);
        var assignmentReason = owner is null
            ? "Unassigned because no active, qualified agent has capacity."
            : owner.Agent.Id == currentAgentId ? $"Kept {owner.Agent.FullName}; still eligible."
            : $"Assigned to {owner.Agent.FullName}: fewest open tickets ({owner.OpenTicketCount}), then lowest agent ID.";

        return new TriageDecision(priority, owner?.Agent.Id, window, priorityReason,
            $"{tier} customer: {window.TotalHours:0.##} hour SLA window.", assignmentReason);
    }
}

public sealed record TriageDecision(
    TicketPriority Priority,
    int? AssignedAgentId,
    TimeSpan Window,
    string PriorityReason,
    string SlaReason,
    string AssignmentReason);
