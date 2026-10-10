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
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

// Run query tests with SQLite.
public class TicketQueriesTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly List<string> _commands = [];
    private readonly Customer _contoso = new("Contoso", "help@contoso.test", null, CustomerTier.Premium, FixedClock.DefaultNow);
    private readonly Customer _fabrikam = new("Fabrikam", "help@fabrikam.test", null, CustomerTier.Standard, FixedClock.DefaultNow);
    private readonly Category _general = new("General", false, false);
    private readonly Category _billing = new("Billing", true, false);
    private readonly Agent _alex = new("Alex", "alex@example.test", 10, FixedClock.DefaultNow);
    private readonly Agent _ben = new("Ben", "ben@example.test", 8, FixedClock.DefaultNow);
    private SupportDbContext _db = null!;
    private TicketQueries _queries = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .LogTo(_commands.Add, [RelationalEventId.CommandExecuted])
            .Options;
        _db = new SupportDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _db.AddRange(_contoso, _fabrikam, _general, _billing, _alex, _ben);
        await _db.SaveChangesAsync();

        _db.AddRange(
            CreateTicket(1, "Router connection drops", _contoso, _general, TicketPriority.High, agent: _alex),
            CreateTicket(2, "Login request", _contoso, _general, TicketPriority.High, TicketStatus.InProgress),
            CreateTicket(3, "Invoice mismatch", _fabrikam, _billing, TicketPriority.Medium, TicketStatus.InProgress, _ben),
            CreateTicket(4, "Invoice received", _fabrikam, _billing, TicketPriority.Low, TicketStatus.Resolved, _alex),
            CreateTicket(5, "Delivery question", _fabrikam, _general, TicketPriority.High, agent: _ben),
            CreateTicket(6, "Critical outage", _contoso, _billing, TicketPriority.Critical, TicketStatus.Open));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        _commands.Clear();
        _queries = new TicketQueries(_db, new FixedClock());
    }

    [Theory]
    [InlineData("status", new[] { "TCK-0002", "TCK-0003" })]
    [InlineData("priority", new[] { "TCK-0001", "TCK-0002", "TCK-0005" })]
    [InlineData("category", new[] { "TCK-0003", "TCK-0004", "TCK-0006" })]
    [InlineData("customer", new[] { "TCK-0001", "TCK-0002", "TCK-0006" })]
    [InlineData("agent", new[] { "TCK-0001", "TCK-0004" })]
    [InlineData("unassigned", new[] { "TCK-0002", "TCK-0006" })]
    public async Task ExactMatchFilters_ReturnOnlyMatchingTickets(string filter, string[] expected)
    {
        var query = new TicketQuery { SortDirection = "asc" };
        query = filter switch
        {
            "status" => query with { Status = TicketStatus.InProgress },
            "priority" => query with { Priority = TicketPriority.High },
            "category" => query with { CategoryId = _billing.Id },
            "customer" => query with { CustomerId = _contoso.Id },
            "agent" => query with { AssignedAgentId = _alex.Id },
            "unassigned" => query with { UnassignedOnly = true },
            _ => throw new ArgumentException("Unknown filter.", nameof(filter))
        };

        var result = await _queries.GetPagedAsync(query, default);

        Assert.Equal(expected, result.Items.Select(ticket => ticket.Reference));
        Assert.Equal(expected.Length, result.TotalCount);
    }

    [Fact]
    public async Task SuppliedFilters_CombineWithAnd()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery
        {
            Status = TicketStatus.InProgress,
            Priority = TicketPriority.High,
            CategoryId = _general.Id,
            CustomerId = _contoso.Id,
            UnassignedOnly = true,
            Search = "Login"
        }, default);

        Assert.Equal("TCK-0002", Assert.Single(result.Items).Reference);
        Assert.Equal(1, result.TotalCount);
    }

    [Theory]
    [InlineData("  Contoso  ", new[] { "TCK-0001", "TCK-0002", "TCK-0006" })]
    [InlineData("Router", new[] { "TCK-0001" })]
    [InlineData("TCK-0003", new[] { "TCK-0003" })]
    public async Task Search_MatchesCustomerNameTitleOrReference(string search, string[] expected)
    {
        var result = await _queries.GetPagedAsync(new TicketQuery
        {
            Search = search,
            SortDirection = "asc"
        }, default);

        Assert.Equal(expected, result.Items.Select(ticket => ticket.Reference));
        Assert.Equal(expected.Length, result.TotalCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task BlankSearch_IsIgnored(string search)
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { Search = search }, default);

        Assert.Equal(6, result.TotalCount);
        Assert.Equal(6, result.Items.Count);
    }

    [Fact]
    public async Task CountAndPaging_UseTheFilteredSetInSql()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery
        {
            Priority = TicketPriority.High,
            Page = 2,
            PageSize = 1,
            SortDirection = "asc"
        }, default);

        Assert.Equal("TCK-0002", Assert.Single(result.Items).Reference);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, _commands.Count);
        Assert.All(_commands, sql => Assert.Contains("WHERE", sql));
        Assert.Contains("COUNT(*)", _commands[0]);
        Assert.Contains("LIMIT", _commands[1]);
        Assert.Empty(_db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task PageBeyondTheResults_IsEmptyButKeepsTheFilteredCount()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery
        {
            Priority = TicketPriority.High,
            Page = 4,
            PageSize = 1
        }, default);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task AssignedAgentAndUnassignedOnly_ReturnNoMatches()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery
        {
            AssignedAgentId = _alex.Id,
            UnassignedOnly = true
        }, default);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static Ticket CreateTicket(
        int number, string title, Customer customer, Category category, TicketPriority priority,
        TicketStatus status = TicketStatus.New, Agent? agent = null)
    {
        var now = FixedClock.DefaultNow.AddMinutes(number);
        var ticket = Ticket.Raise($"TCK-{number:D4}", title, "A test ticket description.", customer.Id, category.Id, priority, now);
        ticket.ChangeStatus(status, now);
        ticket.AssignTo(agent?.Id, now);
        return ticket;
    }
}
