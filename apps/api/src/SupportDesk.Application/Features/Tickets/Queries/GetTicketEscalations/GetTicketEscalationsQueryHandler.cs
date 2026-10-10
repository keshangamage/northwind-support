using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

public sealed class GetTicketEscalationsQueryHandler(ITicketQueries queries)
{
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(int id, CancellationToken ct)
    {
        if (await queries.GetDetailAsync(id, ct) is null)
        {
            throw new NotFoundException("Ticket", id);
        }

        return await queries.GetEscalationsAsync(id, ct);
    }
}
