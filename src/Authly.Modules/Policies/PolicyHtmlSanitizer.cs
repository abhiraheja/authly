using System.Text.RegularExpressions;

namespace Authly.Modules.Policies;

/// <summary>
/// Conservative, defense-in-depth sanitizer for admin-pasted policy HTML. It strips the obvious
/// script vectors (script/style/iframe/object/embed tags, <c>on*</c> event handlers, and
/// <c>javascript:</c> URLs) before storage. The PRIMARY safety control is at render time: policy
/// HTML is shown inside a sandboxed iframe (no allow-scripts), so any markup that slips through still
/// cannot execute. Replace with a full allowlist sanitizer (e.g. Ganss.Xss) when the package feed allows.
/// </summary>
public static partial class PolicyHtmlSanitizer
{
    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";

        var cleaned = html;
        cleaned = DangerousElements().Replace(cleaned, "");   // <script>…</script>, <style>, <iframe>, …
        cleaned = EventHandlers().Replace(cleaned, "");       // on*="…" attributes
        cleaned = JavascriptUrls().Replace(cleaned, "$1=\"#\""); // href/src="javascript:…"
        return cleaned.Trim();
    }

    // Whole dangerous elements (open tag … close tag), case-insensitive, across newlines.
    [GeneratedRegex(@"<\s*(script|style|iframe|object|embed|form|link|meta)\b[^>]*>.*?<\s*/\s*\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DangerousElements();

    // Inline event handlers: on<click|load|error|…>="…" or '…'
    [GeneratedRegex(@"\s+on\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex EventHandlers();

    // href/src pointing at a javascript: URL → neutralize.
    [GeneratedRegex(@"\b(href|src)\s*=\s*""\s*javascript:[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex JavascriptUrls();
}
