using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Infrastructure.Data;
using Xunit;

namespace SupportDesk.IntegrationTests;

public class AuthenticationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Email = "alex.turner@northwind-support.example";

    [Theory]
    [InlineData("/api/tickets")]
    [InlineData("/api/tickets/1")]
    [InlineData("/api/tickets/1/escalations")]
    [InlineData("/api/customers")]
    [InlineData("/api/customers/1")]
    [InlineData("/api/agents")]
    [InlineData("/api/categories")]
    [InlineData("/api/auth/me")]
    public async Task ExistingEndpoint_RequiresAToken(string path)
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        var session = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsTokenExpiryAndCurrentUser()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("  ALEX.TURNER@NORTHWIND-SUPPORT.EXAMPLE  ", SupportDbSeeder.DevelopmentPassword));
        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal("Alex Turner", session.User.FullName);
        Assert.Equal(Email, session.User.Email);
        Assert.InRange(session.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        var me = await client.GetFromJsonAsync<AuthUserDto>("/api/auth/me");
        Assert.Equal(session.User, me);
    }

    [Theory]
    [InlineData("missing@example.com", "Test@1234")]
    [InlineData(Email, "WrongPassword")]
    [InlineData("yuki.tanaka@northwind-support.example", "Test@1234")]
    public async Task Login_RejectsCredentialsWithTheSameMessage(string email, string password)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Email or password is incorrect.", body.GetProperty("detail").GetString());
        Assert.False(body.TryGetProperty("token", out _));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("not-an-email", "Password")]
    public async Task Login_ValidatesInput(string email, string password)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("audience")]
    [InlineData("issuer")]
    [InlineData("signature")]
    [InlineData("missing-name")]
    public async Task ProtectedEndpoint_RejectsInvalidTokens(string failure)
    {
        using var client = factory.CreateClient();
        var session = await LoginAsync(client);
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(session.Token).Claims
            .Where(c => c.Type == ClaimTypes.NameIdentifier || c.Type == ClaimTypes.Name || c.Type == ClaimTypes.Email)
            .Where(c => failure != "missing-name" || c.Type != ClaimTypes.Name);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            failure == "signature" ? "another-test-only-key-with-more-than-32-bytes" : ApiFactory.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            failure == "issuer" ? "OtherIssuer" : "NorthwindSupport",
            failure == "audience" ? "OtherAudience" : "NorthwindSupportWeb",
            claims, DateTime.UtcNow.AddHours(-2),
            failure == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5), credentials);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        var response = await client.GetAsync("/api/tickets");
        Assert.Equal(failure == "missing-name" ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Escalation_UsesTokenActorAndIgnoresBodyActor()
    {
        using var client = factory.CreateClient();
        var session = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        var created = await client.PostAsJsonAsync("/api/tickets", new
        {
            title = "Authentication test",
            description = "Verify the escalation actor comes from the token.",
            customerId = 2,
            categoryId = 1,
            requestedPriority = "Low"
        });
        created.EnsureSuccessStatusCode();
        var ticket = await created.Content.ReadFromJsonAsync<JsonElement>();
        var response = await client.PostAsJsonAsync($"/api/tickets/{ticket.GetProperty("id").GetInt32()}/escalate",
            new { reason = "Customer is blocked", escalatedBy = "Forged actor" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Alex Turner", body.GetProperty("escalation").GetProperty("escalatedBy").GetString());
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Email, SupportDbSeeder.DevelopmentPassword));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }
}
