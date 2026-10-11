using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    public Task<Agent?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Set<Agent>().AsNoTracking().FirstOrDefaultAsync(a => a.Email == email, ct);

    public async Task<IReadOnlyList<AgentWorkload>> GetWorkloadsAsync(int? excludedTicketId, CancellationToken ct) =>
        await db.Set<Agent>()
            .AsNoTracking()
            .Include(a => a.Specializations)
            .Select(a => new AgentWorkload(a, db.Set<Ticket>().Count(t =>
                t.AssignedAgentId == a.Id && t.Id != excludedTicketId
                && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)))
            .ToListAsync(ct);

    /// <summary>Loads the whole aggregate, specializations included.</summary>
    public Task<Agent?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Set<Agent>()
            .Include(a => a.Specializations)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
}
