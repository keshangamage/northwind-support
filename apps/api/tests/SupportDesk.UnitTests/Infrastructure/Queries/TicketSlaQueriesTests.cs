using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public class TicketSlaQueriesTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly List<string> _sql = [];
    private readonly FixedClock _clock = new();
    private SupportDbContext _db = null!;
    private TicketQueries _queries = null!;
    private Customer _customer = null!;
    private Category _category = null!;
    private Agent _agent = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection).LogTo(_sql.Add, [RelationalEventId.CommandExecuted]).Options);
        await _db.Database.EnsureCreatedAsync();
        _customer = new Customer("Contoso", "help@example.com", null, CustomerTier.Standard, _clock.UtcNow);
        _category = new Category("Specialist category", true, false);
        _agent = new Agent("Alex", "alex@example.com", 10, _clock.UtcNow);
        _db.AddRange(_customer, _category, _agent);
        await _db.SaveChangesAsync();
        _agent.AddSpecialization(_category.Id);

        var within = AddTicket(1, 0, priority: TicketPriority.Medium);
        within.AssignTo(_agent.Id, _clock.UtcNow);
        AddTicket(2, -3);
        AddTicket(3, -4);
        AddTicket(4, -5);
        AddTicket(5, -5).ChangeStatus(TicketStatus.Resolved, _clock.UtcNow.AddHours(-1));
        AddTicket(6, -5).ChangeStatus(TicketStatus.Closed, _clock.UtcNow);
        AddTicket(7, 0, withSla: false);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        _queries = new TicketQueries(_db, _clock, TestSlaPolicy.Create());
        _sql.Clear();
    }

    [Theory]
    [InlineData(SlaStatus.WithinSla, 1)]
    [InlineData(SlaStatus.AtRisk, 2)]
    [InlineData(SlaStatus.Breached, 2)]
    [InlineData(SlaStatus.Met, 1)]
    [InlineData(SlaStatus.NotApplicable, 1)]
    public async Task SlaFilter_RunsInSqlBeforeCountAndPaging(SlaStatus status, int expected)
    {
        var result = await _queries.GetPagedAsync(new TicketQuery
        {
            SlaStatus = status,
            PageSize = 1,
            SortBy = "slaStatus"
        }, default);
        Assert.Equal(expected, result.TotalCount);
        Assert.Equal(expected, result.TotalPages);
        Assert.Equal(status, Assert.Single(result.Items).SlaStatus);
        Assert.Equal(2, _sql.Count);
        Assert.All(_sql, command => { Assert.Contains("WHERE", command); Assert.Contains("CASE", command); });
        Assert.Contains("COUNT(*)", _sql[0]);
        Assert.Contains("LIMIT", _sql[1]);
        Assert.Empty(_db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task SlaFilter_CombinesWithOtherFilters()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery
        {
            SlaStatus = SlaStatus.AtRisk,
            Priority = TicketPriority.High,
            CustomerId = _customer.Id,
            Search = "TCK-0002"
        }, default);
        Assert.Equal("TCK-0002", Assert.Single(result.Items).Reference);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task ListDetailAndCustomerTickets_UseTheSameSlaRules()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery(), default);
        var customerTickets = await _queries.GetForCustomerAsync(_customer.Id, default);
        foreach (var row in result.Items)
        {
            var detail = await _queries.GetDetailAsync(row.Id, default);
            Assert.Equal(row.SlaStatus, detail!.SlaStatus);
            Assert.Equal(row.SlaStatus, customerTickets.Single(t => t.Id == row.Id).SlaStatus);
        }
    }

    [Fact]
    public async Task History_IsPersistedAndReturnedNewestFirst()
    {
        var ticket = await _db.Set<Ticket>().FirstAsync(t => t.Reference == "TCK-0001");
        var triage = new TicketTriage(TestSlaPolicy.Create());
        ticket.Escalate(triage.EscalationDecision(ticket, _category, _customer.Tier, []),
            "Customer needs help", "Lead", _clock.UtcNow);
        await _db.SaveChangesAsync();
        var first = Assert.Single(await _queries.GetEscalationsAsync(ticket.Id, default));
        Assert.Equal(_agent.Id, first.FromAgent?.Id);
        Assert.Null(first.ToAgent);
        Assert.Equal(TicketPriority.Medium, first.FromPriority);
        Assert.Equal(TicketPriority.High, first.ToPriority);
        Assert.Equal(_clock.UtcNow.AddHours(8), first.ToDueAtUtc);
        ticket.Escalate(triage.EscalationDecision(ticket, _category, _customer.Tier, []),
            "Still needs help", "Another lead", _clock.UtcNow.AddHours(1));
        await _db.SaveChangesAsync();
        var history = await _queries.GetEscalationsAsync(ticket.Id, default);
        Assert.Equal("Another lead", history[0].EscalatedBy);
        Assert.Equal(first, history[1]);
        _db.ChangeTracker.Clear();
        var saved = await _db.Set<Ticket>().Include(t => t.Escalations).SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(2, saved.Escalations.Count);
        Assert.Equal(TicketPriority.Critical, saved.Priority);
    }

    [Fact]
    public async Task Workloads_CountOnlyOpenTicketsAndCanExcludeTheCurrentTicket()
    {
        var current = await _db.Set<Ticket>().FirstAsync(t => t.Reference == "TCK-0001");
        var resolved = await _db.Set<Ticket>().FirstAsync(t => t.Reference == "TCK-0005");
        resolved.AssignTo(_agent.Id, _clock.UtcNow);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var repository = new AgentRepository(_db);
        var workloads = await repository.GetWorkloadsAsync(null, default);
        var workload = Assert.Single(workloads);
        Assert.Equal(1, workload.OpenTicketCount);
        Assert.Equal(_category.Id, Assert.Single(workload.Agent.Specializations).CategoryId);
        Assert.Equal(0, Assert.Single(await repository.GetWorkloadsAsync(current.Id, default)).OpenTicketCount);
        Assert.Empty(_db.ChangeTracker.Entries());
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private Ticket AddTicket(int number, int hoursFromNow, bool withSla = true, TicketPriority priority = TicketPriority.High)
    {
        var start = _clock.UtcNow.AddHours(hoursFromNow);
        var ticket = Ticket.Raise($"TCK-{number:D4}", "A test ticket", "A description for testing.",
            _customer.Id, _category.Id, priority, start);
        if (withSla) ticket.ApplyTriage(new TriageDecision(priority, null, TimeSpan.FromHours(4), "", "", ""), start);
        _db.Add(ticket);
        return ticket;
    }
}
