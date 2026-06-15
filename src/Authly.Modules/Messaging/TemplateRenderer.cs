using System.Text.RegularExpressions;

namespace Authly.Modules.Messaging;

/// <summary>
/// Substitutes <c>{{variable}}</c> placeholders in a template with supplied values. Pure and
/// side-effect-free. HTML bodies get their values HTML-encoded (defence against injected markup
/// in a user-controlled value); plain-text bodies are substituted verbatim. Unknown placeholders
/// are left as-is so a missing variable is visible rather than silently blanked.
/// </summary>
public static class TemplateRenderer
{
    private static readonly Regex Placeholder = new(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}", RegexOptions.Compiled);

    public static string Render(string template, IReadOnlyDictionary<string, string> variables, bool htmlEncode)
    {
        if (string.IsNullOrEmpty(template)) return template ?? string.Empty;

        return Placeholder.Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            if (!variables.TryGetValue(name, out var value))
                return match.Value; // leave {{unknown}} intact
            return htmlEncode ? System.Net.WebUtility.HtmlEncode(value) : value;
        });
    }

    /// <summary>The distinct placeholder names referenced by a template (for the editor/preview help).</summary>
    public static IReadOnlyList<string> Placeholders(string template)
        => string.IsNullOrEmpty(template)
            ? Array.Empty<string>()
            : Placeholder.Matches(template).Select(m => m.Groups[1].Value).Distinct().ToList();
}
