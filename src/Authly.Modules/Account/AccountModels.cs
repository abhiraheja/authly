namespace Authly.Modules.Account;

/// <summary>End-user-editable profile fields (the user owns these).</summary>
public sealed record ProfileUpdate(string? FirstName, string? LastName, string Timezone, string Locale);

/// <summary>Outcome of an authenticated password change.</summary>
public enum PasswordChangeResult
{
    Success,

    /// <summary>The supplied current password did not match.</summary>
    WrongCurrentPassword,

    /// <summary>The user was not found in the tenant.</summary>
    UserNotFound
}
