namespace Authly.Modules.Messaging;

/// <summary>One positional parameter in a WhatsApp template body ({{1}}, {{2}}, …) and the Authly
/// send-time variable that fills it.</summary>
public sealed record WhatsAppTemplateParam(int Position, string Variable, string Description, string Sample);

/// <summary>
/// The platform's prescribed WhatsApp template for a message key. WhatsApp/Meta only delivers
/// business-initiated messages (OTP, verification) through a <em>pre-approved</em> template referenced
/// by name + language, with positional <c>{{1}}…{{n}}</c> body parameters — free text is rejected
/// outside the 24-hour service window. This spec is what we show the admin to create in their
/// provider (MSG91 → Meta); once approved they bind the real template name back to Authly.
/// </summary>
public sealed record WhatsAppTemplateSpec(
    string Key,
    string SuggestedName,
    string Category,
    string Language,
    string BodyText,
    IReadOnlyList<WhatsAppTemplateParam> Parameters);

/// <summary>Built-in WhatsApp template specs, keyed by <see cref="MessageTemplateKeys"/>.</summary>
public static class WhatsAppTemplateSpecs
{
    private static readonly Dictionary<string, WhatsAppTemplateSpec> Map = new()
    {
        [MessageTemplateKeys.Otp] = new(
            Key: MessageTemplateKeys.Otp,
            SuggestedName: "authly_otp",
            Category: "AUTHENTICATION",
            Language: "en_US",
            BodyText: "{{1}} is your verification code. For your security, do not share this code.",
            Parameters: new[]
            {
                new WhatsAppTemplateParam(1, "otp", "The one-time verification code", "123456"),
            }),

        [MessageTemplateKeys.VerifyNewContact] = new(
            Key: MessageTemplateKeys.VerifyNewContact,
            SuggestedName: "authly_verify_contact",
            Category: "UTILITY",
            Language: "en",
            BodyText: "Confirm this number for your {{1}} account: {{2}} (expires in {{3}} hours).",
            Parameters: new[]
            {
                new WhatsAppTemplateParam(1, "app_name", "Your app / brand name", "Authly"),
                new WhatsAppTemplateParam(2, "action_url", "The verification link", "https://app.example.com/verify?token=…"),
                new WhatsAppTemplateParam(3, "expiry_hours", "Hours until the link expires", "24"),
            }),
    };

    public static WhatsAppTemplateSpec? Find(string key) => Map.GetValueOrDefault(key);

    public static IReadOnlyList<WhatsAppTemplateSpec> All => Map.Values.ToList();
}
