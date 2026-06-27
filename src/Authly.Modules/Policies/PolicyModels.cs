using Authly.Core.Enums;
using Authly.Core.Policies;

namespace Authly.Modules.Policies;

/// <summary>Admin input to create or edit a policy (meta + draft content + enforcement + targeting).</summary>
public sealed class PolicyEditInput
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public PolicyEnforcementMode EnforcementMode { get; set; } = PolicyEnforcementMode.Mandatory;
    public DateTimeOffset? SkipDeadline { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? CloseDate { get; set; }

    /// <summary>Draft content type. PDF content is set separately via <c>UploadPdfAsync</c>.</summary>
    public PolicyContentType ContentType { get; set; } = PolicyContentType.Html;
    public string? HtmlContent { get; set; }

    public PolicyTargeting Targeting { get; set; } = PolicyTargeting.All();
}

/// <summary>Thrown when a policy can't be published (e.g. no content authored yet).</summary>
public sealed class PolicyInvalidException(string message) : Exception(message);

/// <summary>A single policy the signed-in user must see before proceeding.</summary>
public sealed record PendingPrompt(
    Guid PolicyId,
    Guid VersionId,
    int Version,
    string Title,
    string? Description,
    PolicyContentType ContentType,
    string? HtmlContent,
    Guid? AssetId,
    PolicyEnforcementMode Mode,
    bool HardBlock,
    bool AllowSkip,
    bool AllowReject);

/// <summary>The set of interrupting prompts for the current sign-in context.</summary>
public sealed record PendingPromptSet(IReadOnlyList<PendingPrompt> Prompts)
{
    public bool Any => Prompts.Count > 0;

    /// <summary>True if at least one prompt hard-blocks (only Accept lets the user proceed).</summary>
    public bool HasHardBlock => Prompts.Any(p => p.HardBlock);

    public static readonly PendingPromptSet Empty = new(Array.Empty<PendingPrompt>());
}

/// <summary>Auth-method categories used for targeting (normalized from <c>login_history.method</c>).</summary>
public static class AuthMethodCategories
{
    public const string Password = "password";
    public const string Social = "social";
    public const string Passkey = "passkey";
    public const string Phone = "phone";
    public const string MagicLink = "magic_link";

    /// <summary>Maps a raw <c>login_history.method</c> value to a targeting category.</summary>
    public static string Normalize(string? rawMethod) => (rawMethod ?? "").ToLowerInvariant() switch
    {
        "password" or "password_phone" => Password,
        "passkey" => Passkey,
        "phone_otp" => Phone,
        "magic_link" => MagicLink,
        "" => "",
        _ => Social // any provider name (google, facebook, github, microsoft, custom…)
    };
}
