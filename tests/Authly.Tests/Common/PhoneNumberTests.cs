using Authly.Modules.Common;

namespace Authly.Tests.Common;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("+91 98765 43210", "+919876543210")]
    [InlineData("+1 (555) 000-1111", "+15550001111")]
    [InlineData("9876543210", "9876543210")]
    [InlineData("098765-43210", "09876543210")]
    [InlineData("0091 9876543210", "+919876543210")]   // 00 international prefix → +
    [InlineData("  +44 20 7946 0958 ", "+442079460958")]
    public void Normalizes_to_canonical_form(string input, string expected)
        => Assert.Equal(expected, PhoneNumber.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Passes_through_blank(string? input)
        => Assert.Equal(input, PhoneNumber.Normalize(input));

    [Fact]
    public void Same_number_different_formatting_normalizes_equal()
        => Assert.Equal(PhoneNumber.Normalize("+91-98765-43210"), PhoneNumber.Normalize("+91 98765 43210"));
}
