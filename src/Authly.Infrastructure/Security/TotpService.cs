using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Authly.Core.Interfaces;

namespace Authly.Infrastructure.Security;

/// <summary>
/// RFC 6238 TOTP using the de-facto authenticator defaults: HMAC-SHA1, 6 digits, 30-second
/// steps, Unix epoch T0. Secrets are Base32 (RFC 4648, no padding) so they round-trip through
/// any authenticator app and the <c>otpauth://</c> URI.
/// </summary>
public sealed class TotpService : ITotpService
{
    private const int Digits = 6;
    private const int StepSeconds = 30;
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret()
    {
        // 20 bytes = 160 bits, the SHA-1 block-friendly size recommended by RFC 4226.
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32Encode(bytes);
    }

    public string BuildProvisioningUri(string base32Secret, string accountName, string issuer)
    {
        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        var query =
            $"secret={Uri.EscapeDataString(base32Secret)}" +
            $"&issuer={Uri.EscapeDataString(issuer)}" +
            $"&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
        return $"otpauth://totp/{label}?{query}";
    }

    public bool Verify(string base32Secret, string code, int window = 1)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim();
        if (code.Length != Digits || !code.All(char.IsDigit)) return false;

        byte[] key;
        try { key = Base32Decode(base32Secret); }
        catch (FormatException) { return false; }

        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;
        for (var offset = -window; offset <= window; offset++)
        {
            var candidate = Compute(key, counter + offset);
            // Constant-time compare to avoid leaking how many leading digits matched.
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(candidate), Encoding.ASCII.GetBytes(code)))
                return true;
        }
        return false;
    }

    private static string Compute(byte[] key, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, counterBytes, hash);

        var binaryOffset = hash[^1] & 0x0F;
        var binary =
            ((hash[binaryOffset] & 0x7F) << 24) |
            ((hash[binaryOffset + 1] & 0xFF) << 16) |
            ((hash[binaryOffset + 2] & 0xFF) << 8) |
            (hash[binaryOffset + 3] & 0xFF);

        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString(CultureInfo.InvariantCulture).PadLeft(Digits, '0');
    }

    private static string Base32Encode(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder((data.Length + 4) / 5 * 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }
        if (bitsLeft > 0)
            sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        return sb.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        input = input.Trim().TrimEnd('=').ToUpperInvariant().Replace(" ", "");
        if (input.Length == 0) throw new FormatException("Empty secret.");

        var output = new List<byte>(input.Length * 5 / 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var c in input)
        {
            var value = Base32Alphabet.IndexOf(c);
            if (value < 0) throw new FormatException($"Invalid Base32 character '{c}'.");
            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }
        return output.ToArray();
    }
}
