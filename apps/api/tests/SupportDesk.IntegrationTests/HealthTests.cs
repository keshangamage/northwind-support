using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Presentation.Health;
using Xunit;

namespace SupportDesk.IntegrationTests;

public class HealthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_IsPublicAndChecksTheDatabase()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_IsUnhealthyWhenTheDatabaseCannotBeOpened()
    {
        var missingFile = Path.Combine(Path.GetTempPath(), $"supportdesk-{Guid.NewGuid()}.db");
        await using var db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite($"Data Source={missingFile};Mode=ReadWrite").Options);

        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
