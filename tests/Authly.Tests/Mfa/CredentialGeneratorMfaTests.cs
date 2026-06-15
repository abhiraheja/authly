using System.Text.RegularExpressions;
using Authly.Infrastructure.Security;

namespace Authly.Tests.Mfa;

public class CredentialGeneratorMfaTests
{
    [Fact]
    public void GenerateNumericOtp_has_requested_length_and_is_all_digits()
    {
        var gen = new CredentialGenerator();
        var otp = gen.GenerateNumericOtp(6);
        Assert.Equal(6, otp.Length);
        Assert.True(otp.All(char.IsDigit));
    }

    [Fact]
    public void GenerateBackupCode_matches_xxxxx_xxxxx_shape()
    {
        var gen = new CredentialGenerator();
        var code = gen.GenerateBackupCode();
        Assert.Matches(new Regex("^[a-z2-9]{5}-[a-z2-9]{5}$"), code);
    }

    [Fact]
    public void Backup_codes_are_unique_across_a_batch()
    {
        var gen = new CredentialGenerator();
        var set = new HashSet<string>();
        for (var i = 0; i < 50; i++) set.Add(gen.GenerateBackupCode());
        Assert.Equal(50, set.Count); // overwhelmingly likely with ~50 bits each
    }
}
