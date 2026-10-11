using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    TicketTriage triage,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ITicketQueries queries)
{
    public async Task<EscalateTicketResult> HandleAsync(
        int id, EscalateTicketRequest request, string escalatedBy, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);
        ticket.EnsureCanEscalate();
        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);
        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        // Exclude this ticket when checking capacity.
        var workloads = await agents.GetWorkloadsAsync(id, ct);
        var decision = triage.EscalationDecision(ticket, category, customer.Tier, workloads);
        var escalation = ticket.Escalate(decision, request.Reason, escalatedBy, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);

        var updated = await getTicket.HandleAsync(id, ct);
        var history = await queries.GetEscalationsAsync(id, ct);
        return new EscalateTicketResult(updated, history.Single(row => row.Id == escalation.Id));
    }
}
