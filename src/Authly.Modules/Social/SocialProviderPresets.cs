namespace Authly.Modules.Social;

/// <summary>
/// Built-in defaults for well-known OAuth2/OIDC providers: endpoints, default scopes, and the
/// user-info JSON field names used to map a profile. A tenant only supplies client id/secret for
/// these; a "custom" provider supplies its own endpoints (generic OAuth2/OIDC).
/// </summary>
public sealed record SocialProviderPreset(
    string Key,
    string DisplayName,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string UserInfoEndpoint,
    IReadOnlyList<string> DefaultScopes,
    string IdField,
    string EmailField,
    string? EmailVerifiedField,
    string NameField,
    string? GivenNameField = null,
    string? FamilyNameField = null,
    string? PictureField = null,
    string? LocaleField = null);

public static class SocialProviderPresets
{
    public const string Custom = "custom";

    private static readonly Dictionary<string, SocialProviderPreset> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["google"] = new("google", "Google",
            "https://accounts.google.com/o/oauth2/v2/auth",
            "https://oauth2.googleapis.com/token",
            "https://openidconnect.googleapis.com/v1/userinfo",
            new[] { "openid", "email", "profile" },
            IdField: "sub", EmailField: "email", EmailVerifiedField: "email_verified", NameField: "name",
            GivenNameField: "given_name", FamilyNameField: "family_name", PictureField: "picture", LocaleField: "locale"),

        ["microsoft"] = new("microsoft", "Microsoft",
            "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
            "https://login.microsoftonline.com/common/oauth2/v2.0/token",
            "https://graph.microsoft.com/oidc/userinfo",
            new[] { "openid", "email", "profile" },
            IdField: "sub", EmailField: "email", EmailVerifiedField: null, NameField: "name",
            GivenNameField: "given_name", FamilyNameField: "family_name", PictureField: "picture", LocaleField: "locale"),

        ["github"] = new("github", "GitHub",
            "https://github.com/login/oauth/authorize",
            "https://github.com/login/oauth/access_token",
            "https://api.github.com/user",
            new[] { "read:user", "user:email" },
            IdField: "id", EmailField: "email", EmailVerifiedField: null, NameField: "name",
            PictureField: "avatar_url"),

        ["facebook"] = new("facebook", "Facebook",
            "https://www.facebook.com/v19.0/dialog/oauth",
            "https://graph.facebook.com/v19.0/oauth/access_token",
            "https://graph.facebook.com/me?fields=id,name,first_name,last_name,email,picture.width(256).height(256)",
            new[] { "email", "public_profile" },
            IdField: "id", EmailField: "email", EmailVerifiedField: null, NameField: "name",
            GivenNameField: "first_name", FamilyNameField: "last_name", PictureField: "picture"),
    };

    public static IReadOnlyList<SocialProviderPreset> Known => Presets.Values.ToList();

    public static SocialProviderPreset? Find(string key) => Presets.GetValueOrDefault(key);

    public static bool IsKnown(string key) => Presets.ContainsKey(key);
}
