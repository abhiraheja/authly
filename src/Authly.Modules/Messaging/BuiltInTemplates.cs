using Authly.Core.Enums;

namespace Authly.Modules.Messaging;

/// <summary>Stable template keys for the platform's transactional messages.</summary>
public static class MessageTemplateKeys
{
    public const string VerifyEmail = "verify_email";
    public const string ResetPassword = "reset_password";
    public const string Otp = "otp";
    public const string MagicLink = "magic_link";
    public const string Welcome = "welcome";
    public const string SecurityAlert = "security_alert";

    public static readonly IReadOnlyList<string> All =
        new[] { VerifyEmail, ResetPassword, Otp, MagicLink, Welcome, SecurityAlert };
}

/// <summary>The rendered-ready content of a template (before variable substitution).</summary>
public sealed record TemplateContent(string? Subject, string Body);

/// <summary>
/// The platform's built-in default templates, used when a tenant hasn't overridden a given
/// key/channel/locale. Security-critical bodies always include the actual link/OTP variable so a
/// tenant override that drops it can't silently break the flow (the dispatcher injects defaults).
/// </summary>
public static class BuiltInTemplates
{
    // (key, channel) → content. Locale is "en" for built-ins; tenant overrides add locales.
    private static readonly Dictionary<(string, MessageChannel), TemplateContent> Map = new()
    {
        [(MessageTemplateKeys.VerifyEmail, MessageChannel.Email)] = new(
            "Verify your email address",
            "<p>Hi {{user_name}},</p>" +
            "<p>Confirm your email address to finish setting up your {{app_name}} account:</p>" +
            "<p><a href=\"{{action_url}}\">Verify email</a></p>" +
            "<p>This link expires in {{expiry_hours}} hours. If you didn't create an account, you can ignore this email.</p>"),

        [(MessageTemplateKeys.ResetPassword, MessageChannel.Email)] = new(
            "Reset your password",
            "<p>Hi {{user_name}},</p>" +
            "<p>We received a request to reset your {{app_name}} password. Use the link below to choose a new one:</p>" +
            "<p><a href=\"{{action_url}}\">Reset password</a></p>" +
            "<p>This link expires in {{expiry_hours}} hour(s). If you didn't request this, you can safely ignore this email.</p>"),

        [(MessageTemplateKeys.Otp, MessageChannel.Email)] = new(
            "Your verification code",
            "<p>Hi {{user_name}},</p>" +
            "<p>Your one-time verification code is:</p>" +
            "<p style=\"font-size:24px;font-weight:bold;letter-spacing:3px\">{{otp}}</p>" +
            "<p>It expires in {{expiry_minutes}} minutes. If you didn't try to sign in, you can ignore this email.</p>"),

        [(MessageTemplateKeys.Otp, MessageChannel.WhatsApp)] = new(
            null,
            "{{app_name}}: your verification code is {{otp}}. It expires in {{expiry_minutes}} minutes."),

        [(MessageTemplateKeys.MagicLink, MessageChannel.Email)] = new(
            "Your sign-in link",
            "<p>Hi {{user_name}},</p>" +
            "<p>Use the link below to sign in to {{app_name}}:</p>" +
            "<p><a href=\"{{action_url}}\">Sign in</a></p>" +
            "<p>This link expires in {{expiry_minutes}} minutes and can be used once.</p>"),

        [(MessageTemplateKeys.Welcome, MessageChannel.Email)] = new(
            "Welcome to {{app_name}}",
            "<p>Hi {{user_name}},</p>" +
            "<p>Welcome to {{app_name}}! Your account is ready to use.</p>"),

        [(MessageTemplateKeys.SecurityAlert, MessageChannel.Email)] = new(
            "Security alert for your account",
            "<p>Hi {{user_name}},</p>" +
            "<p>{{message}}</p>" +
            "<p>If this wasn't you, secure your account immediately.</p>"),
    };

    public static TemplateContent? Find(string key, MessageChannel channel)
        => Map.TryGetValue((key, channel), out var content) ? content : null;

    public static IReadOnlyList<(string Key, MessageChannel Channel, TemplateContent Content)> All
        => Map.Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value)).ToList();
}
