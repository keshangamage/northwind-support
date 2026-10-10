using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Start = FixedClock.DefaultNow;

    [Fact]
    public void WithoutDueDate_IsNotApplicable()
    {
        var ticket = new TicketBuilder().Build();
        Assert.Equal(SlaStatus.NotApplicable, Evaluate(ticket, Start));
    }

    [Theory]
    [InlineData(0, SlaStatus.WithinSla)]
    [InlineData(10799, SlaStatus.WithinSla)]
    [InlineData(10800, SlaStatus.AtRisk)]
    [InlineData(14400, SlaStatus.AtRisk)]
    [InlineData(14401, SlaStatus.Breached)]
    public void OpenTicket_UsesExactBoundaries(int seconds, SlaStatus expected)
    {
        var ticket = new TicketBuilder().WithSla(TimeSpan.FromHours(4)).Build();
        Assert.Equal(expected, Evaluate(ticket, Start.AddSeconds(seconds)));
    }

    [Theory]
    [InlineData(-1, SlaStatus.Met)]
    [InlineData(0, SlaStatus.Met)]
    [InlineData(1, SlaStatus.Breached)]
    public void ResolvedTicket_UsesResolutionTime(int secondsAfterDue, SlaStatus expected)
    {
        var ticket = new TicketBuilder().WithSla(TimeSpan.FromHours(4)).Build();
        ticket.ChangeStatus(TicketStatus.Resolved, ticket.DueAtUtc!.Value.AddSeconds(secondsAfterDue));
        Assert.Equal(expected, Evaluate(ticket, Start.AddDays(1)));
    }

    [Fact]
    public void Threshold_CanBeConfigured()
    {
        var ticket = new TicketBuilder().WithSla(TimeSpan.FromHours(4)).Build();
        Assert.Equal(SlaStatus.WithinSla, Evaluate(ticket, Start.AddHours(2), 25));
        Assert.Equal(SlaStatus.AtRisk, Evaluate(ticket, Start.AddHours(2), 50));
    }

    [Fact]
    public void Escalation_UsesTheNewWindow()
    {
        var ticket = new TicketBuilder().WithSla(TimeSpan.FromHours(24)).Build();
        var now = Start.AddDays(2);
        ticket.Escalate(new TriageDecision(TicketPriority.High, null, TimeSpan.FromHours(8), "", "", ""),
            "Customer is blocked", "Team lead", now);
        Assert.Equal(SlaStatus.WithinSla, Evaluate(ticket, now.AddHours(5)));
        Assert.Equal(SlaStatus.AtRisk, Evaluate(ticket, now.AddHours(6)));
        Assert.Equal(SlaStatus.Breached, Evaluate(ticket, now.AddHours(8).AddTicks(1)));
    }

    private static SlaStatus Evaluate(Ticket ticket, DateTime now, double threshold = 25) =>
        SlaEvaluator.Projection(now, threshold).Compile()(ticket).Status;
}
