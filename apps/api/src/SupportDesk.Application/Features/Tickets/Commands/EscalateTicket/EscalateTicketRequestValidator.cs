using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty()
            .Must(reason => reason is not null && reason.Trim().Length is >= 5 and <= 500)
            .WithMessage("Reason must be between 5 and 500 characters.");
        RuleFor(x => x.EscalatedBy).NotEmpty()
            .Must(actor => actor is not null && actor.Trim().Length is >= 1 and <= 100)
            .WithMessage("Escalated by is required and must be at most 100 characters.");
    }
}
