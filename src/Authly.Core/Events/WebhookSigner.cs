using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Authly.Core.Events;

/// <summary>
/// HMAC-SHA256 signing for outbound webhooks and pipeline hooks. The signature covers
/// <c>{timestamp}.{body}</c> so a receiver can reject stale/replayed deliveries by checking the
/// timestamp freshness alongside the signature (replay protection). Pure crypto — no I/O.
/// </summary>
public static class WebhookSigner
{
    /// <summary>Header carrying the unix-seconds timestamp the signature was computed over.</summary>
    public const string TimestampHeader = "X-Authly-Timestamp";

    /// <summary>Header carrying the signature, formatted <c>sha256=&lt;hex&gt;</c>.</summary>
    public const string SignatureHeader = "X-Authly-Signature";

    /// <summary>Header carrying the unique delivery id, for receiver-side idempotency.</summary>
    public const string DeliveryIdHeader = "X-Authly-Delivery";

    /// <summary>Header carrying the event name.</summary>
    public const string EventHeader = "X-Authly-Event";

    /// <summary>Computes <c>sha256=&lt;hex&gt;</c> over <paramref name="timestamp"/>.<paramref name="body"/>.</summary>
    public static string Sign(string secret, string body, DateTimeOffset timestamp)
    {
        var unix = timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signed = $"{unix}.{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signed));
        return "sha256=" + Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Constant-time verification of a signature header against a recomputed signature, also
    /// enforcing that <paramref name="timestamp"/> is within <paramref name="tolerance"/> of
    /// <paramref name="now"/>. Provided for tests and any inbound verification.
    /// </summary>
    public static bool Verify(string secret, string body, DateTimeOffset timestamp, string signatureHeader,
        DateTimeOffset now, TimeSpan tolerance)
    {
        if ((now - timestamp).Duration() > tolerance)
            return false;

        var expected = Sign(secret, body, timestamp);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signatureHeader));
    }
}
