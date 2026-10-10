using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketTriageTests
{
    private readonly SlaPolicy _sla = TestSlaPolicy.Create();

    [Theory]
    [InlineData(TicketPriority.Low, CustomerTier.Standard, 72)]
    [InlineData(TicketPriority.Medium, CustomerTier.Standard, 24)]
    [InlineData(TicketPriority.High, CustomerTier.Standard, 8)]
    [InlineData(TicketPriority.Critical, CustomerTier.Standard, 4)]
    [InlineData(TicketPriority.Low, CustomerTier.Premium, 36)]
    [InlineData(TicketPriority.Medium, CustomerTier.Premium, 12)]
    [InlineData(TicketPriority.High, CustomerTier.Premium, 4)]
    [InlineData(TicketPriority.Critical, CustomerTier.Premium, 2)]
    public void SlaWindow_UsesPriorityAndTier(TicketPriority priority, CustomerTier tier, int hours) =>
        Assert.Equal(TimeSpan.FromHours(hours), _sla.GetWindow(priority, tier));

    [Fact]
    public void PremiumWindow_HasAConfigurableFloor()
    {
        _sla.BaseWindowHours[TicketPriority.Critical] = 1;
        Assert.Equal(TimeSpan.FromHours(1), _sla.GetWindow(TicketPriority.Critical, CustomerTier.Premium));
        _sla.MinimumWindowHours = 2;
        Assert.Equal(TimeSpan.FromHours(2), _sla.GetWindow(TicketPriority.Critical, CustomerTier.Premium));
        _sla.PremiumCustomerMultiplier = 0.75;
        Assert.Equal(TimeSpan.FromHours(6), _sla.GetWindow(TicketPriority.High, CustomerTier.Premium));
    }

    [Theory]
    [InlineData(false, null, TicketPriority.Medium)]
    [InlineData(false, TicketPriority.Low, TicketPriority.Low)]
    [InlineData(false, TicketPriority.High, TicketPriority.High)]
    [InlineData(true, TicketPriority.Low, TicketPriority.Critical)]
    [InlineData(true, null, TicketPriority.Critical)]
    public void Priority_UsesFlagsAndRequestedPriority(bool forced, TicketPriority? requested, TicketPriority expected)
    {
        var category = CategoryFor(false, forced);
        var decision = new TicketTriage(_sla).CreateDecision(category, CustomerTier.Standard, requested, []);
        Assert.Equal(expected, decision.Priority);
        Assert.False(string.IsNullOrWhiteSpace(decision.PriorityReason));
    }

    [Fact]
    public void Assignment_ChoosesFewestOpenThenLowestId()
    {
        var category = CategoryFor(false);
        var owner = TicketAssignment.Select([Workload(3, 2), Workload(2, 2), Workload(1, 5)], category);
        Assert.Equal(2, owner?.Agent.Id);
    }

    [Fact]
    public void Assignment_ExcludesInactiveFullAndNonSpecialists()
    {
        var category = CategoryFor(true);
        var workloads = new[]
        {
            Workload(1, 0, active: false, specialist: true),
            Workload(2, 2, limit: 2, specialist: true),
            Workload(3, 0),
            Workload(4, 1, specialist: true)
        };
        Assert.Equal(4, TicketAssignment.Select(workloads, category)?.Agent.Id);
    }

    [Fact]
    public void Assignment_WithNobodyEligible_IsUnassignedWithReason()
    {
        var decision = new TicketTriage(_sla).CreateDecision(CategoryFor(true), CustomerTier.Standard,
            null, [Workload(1, 0)]);
        Assert.Null(decision.AssignedAgentId);
        Assert.Contains("Unassigned because", decision.AssignmentReason);
        Assert.Equal(TimeSpan.FromHours(24), decision.Window);
    }

    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 2)]
    public void Escalation_KeepsEligibleOwnerOrReassigns(bool active, int expectedOwner)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).AssignedTo(4)
            .WithSla(TimeSpan.FromHours(24)).Build();
        var category = CategoryFor(false);
        var decision = new TicketTriage(_sla).EscalationDecision(ticket, category, CustomerTier.Premium,
            [Workload(4, 5, active: active), Workload(2, 1)]);
        var now = FixedClock.DefaultNow.AddDays(2);
        var oldDue = ticket.DueAtUtc;

        var history = ticket.Escalate(decision, "  Customer is blocked  ", "  Team lead  ", now);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(expectedOwner, ticket.AssignedAgentId);
        Assert.Equal(now.AddHours(4), ticket.DueAtUtc);
        Assert.Equal(14400, ticket.SlaWindowSeconds);
        Assert.Same(history, Assert.Single(ticket.Escalations));
        Assert.Equal(TicketPriority.Medium, history.FromPriority);
        Assert.Equal(TicketPriority.High, history.ToPriority);
        Assert.Equal(4, history.FromAgentId);
        Assert.Equal(expectedOwner, history.ToAgentId);
        Assert.Equal(oldDue, history.FromDueAtUtc);
        Assert.Equal(ticket.DueAtUtc, history.ToDueAtUtc);
        Assert.Equal("Customer is blocked", history.Reason);
        Assert.Equal("Team lead", history.EscalatedBy);
        Assert.Equal(now, history.EscalatedAtUtc);
    }

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalation_RaisesExactlyOneLevel(TicketPriority before, TicketPriority after)
    {
        var ticket = new TicketBuilder().WithPriority(before).Build();
        var decision = new TicketTriage(_sla).EscalationDecision(ticket, CategoryFor(false), CustomerTier.Standard, []);
        ticket.Escalate(decision, "Customer needs help", "Team lead", FixedClock.DefaultNow);
        Assert.Equal(after, ticket.Priority);
        Assert.Null(ticket.AssignedAgentId);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, TicketStatus.New, "Critical")]
    [InlineData(TicketPriority.Medium, TicketStatus.Resolved, "Resolved")]
    [InlineData(TicketPriority.Medium, TicketStatus.Closed, "Closed")]
    public void Escalation_RejectsBlockedTickets(TicketPriority priority, TicketStatus status, string message)
    {
        var ticket = new TicketBuilder().WithPriority(priority).WithStatus(status).Build();
        var exception = Assert.Throws<BusinessRuleViolationException>(() =>
            new TicketTriage(_sla).EscalationDecision(ticket, CategoryFor(false), CustomerTier.Standard, []));
        Assert.Contains(message, exception.Message);
        Assert.Equal(priority, ticket.Priority);
        Assert.Empty(ticket.Escalations);
    }

    [Fact]
    public void Escalation_DoesNotChangeEarlierHistory()
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Low).Build();
        var triage = new TicketTriage(_sla);
        var first = ticket.Escalate(triage.EscalationDecision(ticket, CategoryFor(false), CustomerTier.Standard, []),
            "First escalation", "Lead one", FixedClock.DefaultNow);
        ticket.Escalate(triage.EscalationDecision(ticket, CategoryFor(false), CustomerTier.Standard, []),
            "Second escalation", "Lead two", FixedClock.DefaultNow.AddHours(1));
        Assert.Equal(TicketPriority.Medium, first.ToPriority);
        Assert.Equal("Lead one", first.EscalatedBy);
        Assert.Equal(FixedClock.DefaultNow.AddHours(24), first.ToDueAtUtc);
        Assert.Equal(2, ticket.Escalations.Count);
    }

    [Fact]
    public void InvalidSlaConfiguration_IsRejected()
    {
        _sla.Validate();
        _sla.BaseWindowHours.Remove(TicketPriority.High);
        Assert.Throws<InvalidOperationException>(() => _sla.Validate());
    }

    private static Category CategoryFor(bool specialist, bool forced = false)
    {
        var category = new Category("A configured category", specialist, forced);
        typeof(Category).GetProperty(nameof(Category.Id))!.SetValue(category, 7);
        return category;
    }

    private static AgentWorkload Workload(int id, int count, int limit = 10, bool active = true, bool specialist = false)
    {
        var agent = new Agent($"Agent {id}", $"agent{id}@example.com", limit, FixedClock.DefaultNow);
        typeof(Agent).GetProperty(nameof(Agent.Id))!.SetValue(agent, id);
        if (!active) agent.Deactivate();
        if (specialist) agent.AddSpecialization(7);
        return new AgentWorkload(agent, count);
    }
}
