using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Queries.SearchTickets;

public sealed class TicketQueryValidator : AbstractValidator<TicketQuery>
{
    public TicketQueryValidator()
    {
        RuleFor(x => x.SlaStatus).IsInEnum().When(x => x.SlaStatus is not null);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority is not null);
    }
}
