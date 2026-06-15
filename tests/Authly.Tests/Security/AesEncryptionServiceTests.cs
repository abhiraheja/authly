using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Authly.Infrastructure.Security;

namespace Authly.Tests.Security;

public class AesEncryptionServiceTests
{
    private static AesEncryptionService NewService()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new AesEncryptionService(Options.Create(new EncryptionOptions { Key = key }));
    }

    [Fact]
    public void Encrypt_then_Decrypt_round_trips()
    {
        var svc = NewService();
        const string secret = "totp-seed-or-provider-api-key";

        var cipher = svc.Encrypt(secret);

        Assert.NotEqual(secret, cipher);
        Assert.Equal(secret, svc.Decrypt(cipher));
    }

    [Fact]
    public void Encrypt_uses_random_nonce_so_output_differs_each_call()
    {
        var svc = NewService();
        Assert.NotEqual(svc.Encrypt("same"), svc.Encrypt("same"));
    }

    [Fact]
    public void Decrypt_with_different_key_fails_authentication()
    {
        var cipher = NewService().Encrypt("secret");
        var other = NewService();
        Assert.Throws<AuthenticationTagMismatchException>(() => other.Decrypt(cipher));
    }

    [Fact]
    public void Constructor_rejects_key_that_is_not_32_bytes()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        Assert.Throws<InvalidOperationException>(() =>
            new AesEncryptionService(Options.Create(new EncryptionOptions { Key = shortKey })));
    }
}
