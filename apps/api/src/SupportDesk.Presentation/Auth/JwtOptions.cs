using System.Text;

namespace SupportDesk.Presentation.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public int LifetimeMinutes { get; init; } = 60;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience)
            || Encoding.UTF8.GetByteCount(SigningKey) < 32 || LifetimeMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException(
                "Set Jwt issuer, audience, lifetime (1–1440 minutes) and a signing key of at least 32 bytes.");
        }
    }
}
