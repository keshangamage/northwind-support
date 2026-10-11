using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class PasswordServiceTests
{
    [Fact]
    public void Hash_UsesADifferentSaltForEachPassword()
    {
        var agent = new Agent("Alex", "alex@example.com", 10, DateTime.UtcNow);
        var passwords = new PasswordService();
        var first = passwords.Hash(agent, "Test@1234");
        var second = passwords.Hash(agent, "Test@1234");

        Assert.NotEqual(first, second);
        Assert.DoesNotContain("Test@1234", first);
    }

    [Theory]
    [InlineData("Test@1234", true)]
    [InlineData("WrongPassword", false)]
    public void Verify_AcceptsOnlyTheCorrectPassword(string password, bool expected)
    {
        var agent = new Agent("Alex", "alex@example.com", 10, DateTime.UtcNow);
        var passwords = new PasswordService();
        agent.SetPasswordHash(passwords.Hash(agent, "Test@1234"));

        Assert.Equal(expected, passwords.Verify(agent, password));
    }
}
