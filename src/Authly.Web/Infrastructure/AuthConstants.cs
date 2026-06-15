namespace Authly.Web.Infrastructure;

/// <summary>Authentication scheme names used across the app's surfaces.</summary>
public static class AuthSchemes
{
    /// <summary>Isolated cookie scheme for the platform super-admin surface.</summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>Cookie scheme for tenant end-users (registration / login / portal). Fully separate from super-admin.</summary>
    public const string User = "User";

    /// <summary>Cookie scheme for tenant administrators (manage apps, etc.). Separate from both other surfaces.</summary>
    public const string TenantAdmin = "TenantAdmin";

    /// <summary>Policy scheme for the Management API: forwards to <see cref="ApiKey"/> when an X-API-Key header is present, else to Bearer (OpenIddict validation).</summary>
    public const string Api = "Api";

    /// <summary>X-API-Key authentication scheme for the Management API.</summary>
    public const string ApiKey = "ApiKey";
}

/// <summary>Authorization policy names.</summary>
public static class AuthPolicies
{
    public const string SuperAdmin = "SuperAdminPolicy";
    public const string User = "UserPolicy";
    public const string TenantAdmin = "TenantAdminPolicy";
    public const string Api = "ApiPolicy";
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

    /// <summary>Present only on an impersonation session — the admin user id acting as this user.</summary>
    public const string ImpersonatorId = "authly:impersonator_id";

    /// <summary>The impersonating admin's email, shown in the portal impersonation banner.</summary>
    public const string ImpersonatorEmail = "authly:impersonator_email";
}

/// <summary>Custom claim types for the tenant-admin principal.</summary>
public static class TenantAdminClaims
{
    public const string TenantId = "authly:tenant_id";
}

/// <summary>Claim types carried inside issued OAuth/OIDC tokens (access/identity).</summary>
public static class TokenClaims
{
    /// <summary>Standard role claim ("role"); one claim per role name.</summary>
    public const string Role = "role";

    /// <summary>Flattened permission set; one claim per <c>resource.action</c>.</summary>
    public const string Permissions = "permissions";

    /// <summary>The tenant the token was issued for.</summary>
    public const string TenantId = "tenant_id";
}
