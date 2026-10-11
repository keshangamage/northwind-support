using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-only-signing-key-for-northwind-authentication";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public ApiFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "Server=unused;Database=unused");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<SupportDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SupportDbContext>>();
            services.AddDbContext<SupportDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SupportDbContext>().Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<SupportDbSeeder>().SeedAsync().GetAwaiter().GetResult();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
