namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>
/// Shared model for the <c>_TargetingEditor</c> partial — the audience/targeting block reused by the
/// policy and survey editors (all / applications / sign-in method / provider / role, plus an advanced
/// any/all combination) with an inline "preview audience" action.
/// </summary>
public sealed class TargetingEditorModel
{
    public string Audience { get; set; } = "all";
    public List<Guid> ApplicationIds { get; set; } = new();
    public List<string> AuthMethods { get; set; } = new();
    public List<string> Providers { get; set; } = new();
    public List<string> Roles { get; set; } = new();
    public string Match { get; set; } = "any";

    public List<AppOption> AvailableApps { get; set; } = new();
    public List<string> AvailableProviders { get; set; } = new();
    public List<string> AvailableRoles { get; set; } = new();
    public string[] AuthMethodOptions { get; set; } =
        { "password", "social", "passkey", "phone", "magic_link" };

    /// <summary>The controller action URL the preview button POSTs the current selection to.</summary>
    public string PreviewUrl { get; set; } = "";

    public sealed record AppOption(Guid Id, string Name);
}

/// <summary>Posted by the "preview audience" button to estimate how many users a targeting reaches.</summary>
public sealed class AudiencePreviewForm
{
    public string Audience { get; set; } = "all";
    public List<Guid> ApplicationIds { get; set; } = new();
    public List<string> AuthMethods { get; set; } = new();
    public List<string> Providers { get; set; } = new();
    public List<string> Roles { get; set; } = new();
    public string Match { get; set; } = "any";
}
