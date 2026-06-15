namespace Authly.Core.Enums;

/// <summary>The kind of second factor. Stored as snake_case text ("totp", "email_otp", …).</summary>
public enum MfaFactorType
{
    Totp,
    EmailOtp,
    WhatsAppOtp,
    Passkey
}
