namespace Authly.Web.Infrastructure;

/// <summary>Authentication scheme names used across the app's surfaces.</summary>
public static class AuthSchemes
{
    /// <summary>Isolated cookie scheme for the platform super-admin surface.</summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>Cookie scheme for tenant end-users (registration / login / portal). Fully separate from super-admin.</summary>
    public const string User = "User";
}

/// <summary>Authorization policy names.</summary>
public static class AuthPolicies
{
    public const string SuperAdmin = "SuperAdminPolicy";
    public const string User = "UserPolicy";
}

/// <summary>Custom claim types for the super-admin principal.</summary>
public static class SuperAdminClaims
{
    public const string MustChangePassword = "authly:must_change_password";
}

/// <summary>Custom claim types for the tenant end-user principal.</summary>
public static class UserClaims
{
    public const string TenantId = "authly:tenant_id";
    public const string SessionId = "authly:session_id";
    public const string EmailVerified = "authly:email_verified";
}
