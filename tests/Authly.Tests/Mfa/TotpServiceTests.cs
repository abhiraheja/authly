using System.Security.Cryptography;
using Authly.Infrastructure.Security;

namespace Authly.Tests.Mfa;

public class TotpServiceTests
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    [Fact]
    public void GenerateSecret_is_decodable_base32()
    {
        var svc = new TotpService();
        var secret = svc.GenerateSecret();
        Assert.True(secret.Length >= 16);
        Assert.True(secret.All(c => Base32Alphabet.Contains(c)));
        Assert.NotEmpty(Base32Decode(secret)); // round-trips through a standard Base32 decoder
    }

    [Fact]
    public void ProvisioningUri_carries_issuer_and_secret()
    {
        var svc = new TotpService();
        var secret = svc.GenerateSecret();
        var uri = svc.BuildProvisioningUri(secret, "ada@example.com", "Authly");

        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains("issuer=Authly", uri);
        Assert.Contains($"secret={secret}", uri);
    }

    [Fact]
    public void Verify_accepts_a_correctly_computed_code()
    {
        var svc = new TotpService();
        var secret = svc.GenerateSecret();
        var code = ComputeNow(secret);
        Assert.True(svc.Verify(secret, code));
    }

    [Fact]
    public void Verify_rejects_a_wrong_code()
    {
        var svc = new TotpService();
        var secret = svc.GenerateSecret();
        var correct = ComputeNow(secret);
        // Pick a code guaranteed to differ from the correct one.
        var wrong = correct == "000000" ? "111111" : "000000";
        Assert.False(svc.Verify(secret, wrong));
    }

    [Fact]
    public void Verify_rejects_malformed_input()
    {
        var svc = new TotpService();
        var secret = svc.GenerateSecret();
        Assert.False(svc.Verify(secret, "abc"));
        Assert.False(svc.Verify(secret, ""));
        Assert.False(svc.Verify(secret, "1234567"));
    }

    // --- an independent reference TOTP (RFC 6238: HMAC-SHA1, 6 digits, 30s) ---
    private static string ComputeNow(string base32Secret)
    {
        var key = Base32Decode(base32Secret);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        Span<byte> ctr = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(ctr, counter);
        var hash = HMACSHA1.HashData(key, ctr.ToArray());
        var offset = hash[^1] & 0x0F;
        var bin = ((hash[offset] & 0x7F) << 24) | ((hash[offset + 1] & 0xFF) << 16)
                  | ((hash[offset + 2] & 0xFF) << 8) | (hash[offset + 3] & 0xFF);
        return (bin % 1_000_000).ToString().PadLeft(6, '0');
    }

    private static byte[] Base32Decode(string input)
    {
        input = input.TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in input)
        {
            buffer = (buffer << 5) | Base32Alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8) { bits -= 8; output.Add((byte)((buffer >> bits) & 0xFF)); }
        }
        return output.ToArray();
    }
}
