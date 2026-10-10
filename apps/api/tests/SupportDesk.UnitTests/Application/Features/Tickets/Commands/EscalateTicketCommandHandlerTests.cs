using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    [Fact]
    public async Task Escalate_SavesTicketAndHistoryTogether()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithSla(TimeSpan.FromHours(24)).Build());
        context.ExistingAgent(4);
        context.Clock.Advance(TimeSpan.FromDays(1));

        var result = await context.EscalateTicket.HandleAsync(ticket.Id,
            new EscalateTicketRequest("Customer is blocked", "Team lead"), default);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(context.Clock.UtcNow.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(4, ticket.AssignedAgentId);
        Assert.Equal("Customer is blocked", result.Escalation.Reason);
        Assert.Equal(context.Clock.UtcNow, result.Escalation.EscalatedAtUtc);
        context.Agents.Verify(a => a.GetWorkloadsAsync(ticket.Id, It.IsAny<CancellationToken>()), Times.Once);
        context.VerifySaved(Times.Once());
    }

    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.Open, "Critical")]
    [InlineData(TicketPriority.Low, TicketStatus.Resolved, "Resolved")]
    [InlineData(TicketPriority.Low, TicketStatus.Closed, "Closed")]
    public async Task Escalate_RejectsBlockedTicketsWithoutSaving(TicketPriority priority, TicketStatus status, string message)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithPriority(priority).WithStatus(status).Build());

        var error = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            context.EscalateTicket.HandleAsync(ticket.Id, new EscalateTicketRequest("Please help", "Lead"), default));

        Assert.Contains(message, error.Message);
        Assert.Empty(ticket.Escalations);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Escalate_UnknownTicketIsNotFound()
    {
        var context = new TicketCommandTestContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            context.EscalateTicket.HandleAsync(404, new EscalateTicketRequest("Please help", "Lead"), default));
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData("1234", "Lead", false)]
    [InlineData("     ", "Lead", false)]
    [InlineData("Valid reason", "", false)]
    [InlineData("Valid reason", "   ", false)]
    [InlineData(" 12345 ", " Lead ", true)]
    public void Validator_ChecksTrimmedReasonAndActor(string reason, string actor, bool valid) =>
        Assert.Equal(valid, new EscalateTicketRequestValidator()
            .Validate(new EscalateTicketRequest(reason, actor)).IsValid);

    [Theory]
    [InlineData(500, 100, true)]
    [InlineData(501, 100, false)]
    [InlineData(500, 101, false)]
    public void Validator_ChecksMaximumLengths(int reasonLength, int actorLength, bool valid) =>
        Assert.Equal(valid, new EscalateTicketRequestValidator()
            .Validate(new EscalateTicketRequest(new string('a', reasonLength), new string('b', actorLength))).IsValid);
}
