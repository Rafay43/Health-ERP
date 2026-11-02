using System.Security.Cryptography;
using System.Text;

namespace HealthERP.Application.Common.Security;

public static class JwtSessionVersion
{
    public const string ClaimName = "session_version";

    // Expose a fingerprint, never the Identity security stamp itself.
    public static string FromSecurityStamp(string securityStamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp)));
    }

    public static bool Matches(string? tokenVersion, string? securityStamp)
    {
        if (tokenVersion?.Length != 64 || string.IsNullOrWhiteSpace(securityStamp)) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(tokenVersion),
                SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp)));
        }
        catch (FormatException) { return false; }
    }
}
