using Authly.Infrastructure.Security;

namespace Authly.Tests.Security;

public class Argon2idPasswordHasherTests
{
    private readonly Argon2idPasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_Verify_succeeds_for_correct_password()
    {
        var hash = _hasher.Hash("Sup3r$ecret!");
        Assert.True(_hasher.Verify(hash, "Sup3r$ecret!"));
    }

    [Fact]
    public void Verify_fails_for_wrong_password()
    {
        var hash = _hasher.Hash("Sup3r$ecret!");
        Assert.False(_hasher.Verify(hash, "wrong-password"));
    }

    [Fact]
    public void Hash_is_salted_so_same_password_yields_different_hashes()
    {
        var a = _hasher.Hash("same-password");
        var b = _hasher.Hash("same-password");
        Assert.NotEqual(a, b);
        Assert.True(_hasher.Verify(a, "same-password"));
        Assert.True(_hasher.Verify(b, "same-password"));
    }

    [Fact]
    public void Hash_is_self_describing_with_argon2id_parameters()
    {
        var hash = _hasher.Hash("x");
        var parts = hash.Split('|');
        Assert.Equal(6, parts.Length);
        Assert.Equal("argon2id", parts[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-hash")]
    [InlineData("argon2id|bad|2|1|salt|hash")]
    public void Verify_returns_false_for_malformed_hash(string malformed)
    {
        Assert.False(_hasher.Verify(malformed, "anything"));
    }
}
