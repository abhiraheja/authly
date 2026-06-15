namespace Authly.Web.Infrastructure;

/// <summary>Authentication scheme names used across the app's surfaces.</summary>
public static class AuthSchemes
{
    /// <summary>Isolated cookie scheme for the platform super-admin surface.</summary>
    public const string SuperAdmin = "SuperAdmin";
}

/// <summary>Authorization policy names.</summary>
public static class AuthPolicies
{
    public const string SuperAdmin = "SuperAdminPolicy";
}

/// <summary>Custom claim types for the super-admin principal.</summary>
public static class SuperAdminClaims
{
    public const string MustChangePassword = "authly:must_change_password";
}
