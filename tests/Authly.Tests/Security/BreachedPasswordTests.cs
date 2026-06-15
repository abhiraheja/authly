using System.Security.Cryptography;
using System.Text;
using Authly.Core.Security;
using Authly.Infrastructure.Security;

namespace Authly.Tests.Security;

public class BreachedPasswordTests
{
    private static string Sha1Hex(string s) => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(s)));

    [Fact]
    public async Task Reports_breached_when_the_suffix_appears_with_a_nonzero_count()
    {
        const string pw = "P@ssw0rd";
        var suffix = Sha1Hex(pw)[5..];
        var gateway = new HibpBreachedPasswordGateway(new FakeRange($"{suffix}:42\r\nAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:1"));

        Assert.True(await gateway.IsBreachedAsync(pw));
    }

    [Fact]
    public async Task Treats_a_zero_count_padding_row_as_not_breached()
    {
        const string pw = "P@ssw0rd";
        var suffix = Sha1Hex(pw)[5..];
        var gateway = new HibpBreachedPasswordGateway(new FakeRange($"{suffix}:0"));

        Assert.False(await gateway.IsBreachedAsync(pw));
    }

    [Fact]
    public async Task Returns_false_when_the_suffix_is_absent()
    {
        var gateway = new HibpBreachedPasswordGateway(new FakeRange("0000000000000000000000000000000000A:5"));
        Assert.False(await gateway.IsBreachedAsync("totally-unique-passphrase-9173"));
    }

    [Fact]
    public async Task Fails_open_when_the_range_service_is_unavailable()
    {
        var gateway = new HibpBreachedPasswordGateway(new FakeRange(null));
        Assert.False(await gateway.IsBreachedAsync("anything"));
    }

    private sealed class FakeRange : IPwnedRangeClient
    {
        private readonly string? _body;
        public FakeRange(string? body) => _body = body;
        public Task<string?> GetRangeAsync(string hashPrefix, CancellationToken ct = default) => Task.FromResult(_body);
    }
}
