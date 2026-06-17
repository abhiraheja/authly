namespace Authly.Web.Models;

/// <summary>View models for the public marketing + docs site.</summary>
public sealed record FeatureCardVM(string Icon, string Title, string Desc, string? Hint = null);

/// <summary>One tab of a code sample. <see cref="Html"/> is pre-escaped HTML (may contain token spans).</summary>
public sealed record CodeTab(string Label, string Html);

public sealed record CodeTabs(string Id, IReadOnlyList<CodeTab> Tabs);

/// <summary>A labeled screenshot placeholder the user later replaces with a real image.</summary>
public sealed record ScreenshotVM(string Caption, string FileName, string Aspect = "16/9");

/// <summary>One link in the docs sidebar.</summary>
public sealed record DocsNavItem(string Title, string Href, string Key);
