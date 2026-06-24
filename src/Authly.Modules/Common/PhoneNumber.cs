using System.Text;

namespace Authly.Modules.Common;

/// <summary>
/// Normalizes user-entered phone numbers to a consistent comparable form so the same number typed
/// with spaces, dashes, parentheses or a leading "+"/"00" resolves to one stored value. This is a
/// lightweight E.164-ish canonicalization (not full libphonenumber validation): keep a single
/// leading "+", drop every other non-digit. Used at signup, login lookup and storage.
/// </summary>
public static class PhoneNumber
{
    /// <summary>Returns the normalized number, or null/empty unchanged when there is nothing to normalize.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        var trimmed = raw.Trim();
        // "00<cc>" is the international-access-code form of "+<cc>".
        var plus = trimmed.StartsWith('+') || trimmed.StartsWith("00", StringComparison.Ordinal);

        var digits = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
            if (char.IsDigit(ch))
                digits.Append(ch);

        var body = digits.ToString();
        if (plus && body.StartsWith("00", StringComparison.Ordinal))
            body = body[2..];

        if (body.Length == 0) return string.Empty;
        return plus ? "+" + body : body;
    }
}
