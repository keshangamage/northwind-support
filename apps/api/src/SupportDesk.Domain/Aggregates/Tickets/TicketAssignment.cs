using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Tickets;

public static class TicketAssignment
{
    public static bool IsEligible(AgentWorkload workload, Category category) =>
        workload.Agent.IsActive
        && workload.OpenTicketCount < workload.Agent.MaxOpenTickets
        && (!category.RequiresSpecialist
            || workload.Agent.Specializations.Any(s => s.CategoryId == category.Id));

    public static AgentWorkload? Select(
        IReadOnlyList<AgentWorkload> workloads, Category category, int? currentAgentId = null)
    {
        var eligible = workloads.Where(workload => IsEligible(workload, category));
        var current = eligible.FirstOrDefault(workload => workload.Agent.Id == currentAgentId);

        return current ?? eligible
            .OrderBy(workload => workload.OpenTicketCount)
            .ThenBy(workload => workload.Agent.Id)
            .FirstOrDefault();
    }
}
