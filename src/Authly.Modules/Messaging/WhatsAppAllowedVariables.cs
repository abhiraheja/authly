namespace Authly.Modules.Messaging;

/// <summary>
/// The named-parameter contract for a WhatsApp template bound to an Authly message key. Unlike
/// <see cref="WhatsAppTemplateSpecs"/> (positional <c>{{1}}…{{n}}</c>, legacy), this models the
/// admin-facing flow where the tenant authors their own approved template using <c>{{name}}</c>
/// placeholders that must be drawn ONLY from <see cref="Allowed"/>, and must include
/// <see cref="Required"/>. Positional placeholders are rejected at bind time.
/// </summary>
public sealed record WhatsAppVariableSet(
    string Key,
    string Required,
    IReadOnlyList<string> Allowed,
    string SuggestedName,
    string Category,
    string Language,
    string RecommendedBody,
    IReadOnlyDictionary<string, string> Samples);

/// <summary>
/// The WhatsApp message keys Authly can link a tenant template to, and the named variables each one
/// makes available at send time. The allowed sets are derived from the real send call-sites
/// (<c>MfaService</c> for OTP, <c>ContactChangeService</c> for verify-new-contact) plus the
/// <c>app_name</c>/<c>user_name</c> defaults injected by <c>MessagingService.WithDefaults</c>.
/// </summary>
public static class WhatsAppAllowedVariables
{
    private static readonly Dictionary<string, WhatsAppVariableSet> Map = new()
    {
        [MessageTemplateKeys.Otp] = new(
            Key: MessageTemplateKeys.Otp,
            Required: "otp",
            Allowed: new[] { "otp", "user_name", "expiry_minutes", "app_name" },
            SuggestedName: "authly_otp",
            Category: "AUTHENTICATION",
            Language: "en",
            RecommendedBody: "{{app_name}}: your verification code is {{otp}}. It expires in {{expiry_minutes}} minutes.",
            Samples: new Dictionary<string, string>
            {
                ["otp"] = "123456",
                ["user_name"] = "Sample User",
                ["expiry_minutes"] = "10",
                ["app_name"] = "Authly",
            }),

        [MessageTemplateKeys.VerifyNewContact] = new(
            Key: MessageTemplateKeys.VerifyNewContact,
            Required: "action_url",
            Allowed: new[] { "action_url", "user_name", "expiry_hours", "app_name" },
            SuggestedName: "authly_verify_contact",
            Category: "UTILITY",
            Language: "en",
            RecommendedBody: "{{app_name}}: confirm this number for your account: {{action_url}} (expires in {{expiry_hours}} hours).",
            Samples: new Dictionary<string, string>
            {
                ["action_url"] = "https://app.example.com/verify?token=sample",
                ["user_name"] = "Sample User",
                ["expiry_hours"] = "24",
                ["app_name"] = "Authly",
            }),
    };

    public static WhatsAppVariableSet? Find(string key) => Map.GetValueOrDefault(key);

    public static IReadOnlyList<WhatsAppVariableSet> All => Map.Values.ToList();

    public static bool IsSupportedKey(string key) => Map.ContainsKey(key);
}
