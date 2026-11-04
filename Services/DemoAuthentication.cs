using System.Security.Cryptography;
using System.Text;

namespace GreenAndTasty.Backend.Services;

// Demonstration-only authentication. Replace it with verified JWT/cookies in production.
public sealed class DemoAuthentication(IConfiguration configuration)
{
    private readonly string _ownerToken = configuration["DEMO_OWNER_TOKEN"] ?? "demo-owner-token";
    private readonly string _guestToken = configuration["DEMO_GUEST_TOKEN"] ?? "demo-guest-token";

    public string? GetUserId(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-Demo-Token", out var token))
            return null;

        var raw = token.ToString();
        if (Matches(raw, _ownerToken)) return "owner-user-123";
        if (Matches(raw, _guestToken)) return "guest-user-123";
        return null;
    }

    private static bool Matches(string supplied, string expected)
    {
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
