using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore; // OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Auth;
using Authly.Modules.Authorization;
using Authly.Modules.Claims;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Authly.Web.Controllers.Connect;

/// <summary>
/// OAuth 2.0 / OpenID Connect endpoints (OpenIddict passthrough). Handles the authorization-code
/// hand-off to the branded end-user login, token issuance for all enabled grants with tenant-aware
/// claim assembly (§5.6), userinfo, and end-session. Refresh-token rotation + reuse detection are
/// handled by the OpenIddict server pipeline.
/// </summary>
public sealed class AuthorizationController : Controller
{
    private readonly IApplicationRepository _applications;
    private readonly IUserRepository _users;
    private readonly IRbacService _rbac;
    private readonly ITokenClaimAssembler _claims;
    private readonly ITenantContext _tenant;
    private readonly IAuthService _auth;

    public AuthorizationController(IApplicationRepository applications, IUserRepository users, IRbacService rbac,
        ITokenClaimAssembler claims, ITenantContext tenant, IAuthService auth)
    {
        _applications = applications;
        _users = users;
        _rbac = rbac;
        _claims = claims;
        _tenant = tenant;
        _auth = auth;
    }

    // --- Authorization endpoint (Authorization Code + PKCE) ---

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize(CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        // The client must belong to a tenant we know about.
        var application = await _applications.GetByClientIdAsync(request.ClientId!, ct);
        if (application is null)
            return Forbid(properties: ErrorProps(Errors.InvalidClient, "Unknown client."),
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // Fail fast on a tenant/client mismatch: if the request resolved a tenant (custom domain or
        // the dev ?tenant= override) that isn't the client's own tenant, reject *before* rendering
        // another workspace's branded login. The post-login claim check below is the hard guarantee;
        // this just avoids showing a login page the request can never complete.
        if (_tenant.HasTenant && _tenant.TenantId != application.TenantId)
            return Forbid(properties: ErrorProps(Errors.InvalidRequest, "This application belongs to a different workspace."),
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // Require an authenticated end-user; otherwise hand off to the branded login and return here.
        var auth = await HttpContext.AuthenticateAsync(AuthSchemes.User);
        if (!auth.Succeeded || auth.Principal?.Identity?.IsAuthenticated != true)
        {
            var parameters = Request.HasFormContentType
                ? (IEnumerable<KeyValuePair<string, StringValues>>)Request.Form
                : Request.Query;

            return Challenge(
                authenticationSchemes: AuthSchemes.User,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + QueryString.Create(parameters)
                });
        }

        // Cross-tenant guard: a user authenticated for tenant A may not authorize tenant B's client.
        var userTenant = auth.Principal.FindFirstValue(UserClaims.TenantId);
        if (!Guid.TryParse(userTenant, out var tenantId) || tenantId != application.TenantId)
            return Forbid(properties: ErrorProps(Errors.AccessDenied, "This account cannot sign in to this application."),
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var userId = auth.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _users.GetByIdAsync(application.TenantId, Guid.Parse(userId), ct);
        if (user is null)
            return Forbid(properties: ErrorProps(Errors.AccessDenied, "Account not found."),
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // Bind the issued tokens to this login session (the `sid` claim). Revoking that session —
        // from the portal, or via the cascade when the user is suspended/deleted — then invalidates the
        // tokens at the next refresh (see ExchangeUserGrantAsync). Absent for logins with no session row.
        var sessionId = auth.Principal.FindFirstValue(UserClaims.SessionId);

        var (identity, blocked, reason, idClaimNames) =
            await BuildUserIdentityAsync(application, user, request.GetScopes(), sessionId, ct);
        if (blocked)
            return Forbid(properties: ErrorProps(Errors.AccessDenied, reason ?? "Token issuance was blocked by a pipeline hook."),
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        identity.SetDestinations(claim => DestinationsFor(claim, idClaimNames));

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // --- Token endpoint (all grant types) ---

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange(CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsClientCredentialsGrantType())
            return await ExchangeClientCredentialsAsync(request, ct);

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
            return await ExchangeUserGrantAsync(request, ct);

        throw new InvalidOperationException("The specified grant type is not supported.");
    }

    private async Task<IActionResult> ExchangeClientCredentialsAsync(OpenIddictRequest request, CancellationToken ct)
    {
        // Machine-to-machine: the subject is the client itself, scoped to the client's tenant.
        var application = await _applications.GetByClientIdAsync(request.ClientId!, ct)
            ?? throw new InvalidOperationException("The client application cannot be found.");

        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParametersScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, application.ClientId)
            .SetClaim(Claims.Name, application.Name)
            .SetClaim(TenantClaim, application.TenantId.ToString());

        // Machine-to-machine tokens also get tenant custom claims + pre-token hook claims (no user
        // metadata to map, but static claims and webhook claims still apply).
        // `parameters` forwards the caller's NON-STANDARD token-request parameters to the pre-token hook
        // as an opaque bag. Authly attaches no meaning to any of them — a hook decides what (if anything)
        // a given key means (e.g. a multi-tenant app naming the account a machine token acts for). Values
        // are never trusted as claims directly; a hook that ignores them yields no extra claims.
        var payload = new
        {
            sub = application.ClientId,
            client_id = application.ClientId,
            tenant_id = application.TenantId.ToString(),
            scopes = request.GetScopes().ToArray(),
            parameters = CustomTokenParameters(request)
        };
        var (blocked, reason, idClaimNames) = await ApplyCustomClaimsAsync(
            identity, application.TenantId, application.Id, userMetadataJson: null, appMetadataJson: null, payload, ct);
        if (blocked)
            return Forbid(properties: ErrorProps(Errors.AccessDenied, reason ?? "Token issuance was blocked by a pipeline hook."),
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        identity.SetDestinations(claim => DestinationsFor(claim, idClaimNames));

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // Standard OAuth 2.0 / OIDC token-endpoint request parameters. These are protocol/credential fields
    // Authly handles itself — they are NEVER forwarded to a pre-token hook (client_secret must not leak,
    // and the rest carry no application meaning). Everything else a caller sends is treated as opaque.
    private static readonly HashSet<string> ReservedTokenParameters = new(StringComparer.Ordinal)
    {
        Parameters.GrantType, Parameters.ClientId, Parameters.ClientSecret, Parameters.ClientAssertion,
        Parameters.ClientAssertionType, Parameters.Code, Parameters.CodeVerifier, Parameters.RedirectUri,
        Parameters.RefreshToken, Parameters.Scope, Parameters.Resource, Parameters.Audience,
        Parameters.Username, Parameters.Password, Parameters.Assertion, Parameters.DeviceCode,
    };

    // Collects the caller's NON-STANDARD token-request parameters into an opaque bag forwarded to the
    // pre-token hook. Authly assigns no meaning to any key — a hook interprets them (e.g. resolving the
    // account a multi-tenant machine token acts for). Reserved protocol/credential parameters are excluded
    // so no secret or protocol field ever reaches hook input.
    private static Dictionary<string, string> CustomTokenParameters(OpenIddictRequest request)
    {
        var custom = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in request.GetParameters())
        {
            if (ReservedTokenParameters.Contains(parameter.Key))
                continue;
            var value = (string?)parameter.Value;
            if (!string.IsNullOrEmpty(value))
                custom[parameter.Key] = value;
        }
        return custom;
    }

    private async Task<IActionResult> ExchangeUserGrantAsync(OpenIddictRequest request, CancellationToken ct)
    {
        // Principal restored from the authorization code / refresh token by OpenIddict.
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result.Principal
            ?? throw new InvalidOperationException("The stored principal cannot be retrieved.");

        var tenant = principal.FindFirstValue(TenantClaim);
        var subject = principal.FindFirstValue(Claims.Subject);
        var sessionGuid = Guid.TryParse(principal.FindFirstValue(SessionIdClaim), out var parsedSid)
            ? parsedSid : (Guid?)null;

        // Without an identifiable account there's nothing to re-check (defensive — user grants always
        // carry sub + tenant). Pass the stored principal through unchanged.
        if (!Guid.TryParse(tenant, out var tenantId) || !Guid.TryParse(subject, out var userId))
            return PassThrough(principal);

        // A PKCE/refresh token exchange is a cookie-less form POST, so TenantResolutionMiddleware can't
        // resolve the tenant (it reads host/header/query/cookie, never the form body). Set it from the
        // token's own tenant claim so the RLS backstop (app.current_tenant) lets the lookups see the row.
        _tenant.SetTenant(tenantId);

        // 1. Account still active? A suspended/deleted user must lose access at the next refresh.
        var user = await _users.GetByIdAsync(tenantId, userId, ct);
        if (user is null || user.Status is UserStatus.Suspended or UserStatus.Deleted)
            return Forbid(
                properties: ErrorProps(Errors.InvalidGrant, "The account is no longer active."),
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // 2. Session still alive? Enforced only when the token carries a session id — tokens minted
        // before session binding fall back to the account check above. A revoked session (portal
        // "sign out device", or the cascade on user suspend/delete) fails the refresh here.
        if (sessionGuid is { } sid && await _auth.GetActiveSessionAsync(sid, ct) is null)
            return Forbid(
                properties: ErrorProps(Errors.InvalidGrant, "The session is no longer active."),
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // 3. On refresh, re-derive authorization from current state so a role/permission/company change
        // takes effect within the access-token lifetime. (The auth-code exchange already carries fresh
        // claims straight from Authorize, so only the refresh grant needs the rebuild.)
        if (request.IsRefreshTokenGrantType())
        {
            var application = await _applications.GetByClientIdAsync(request.ClientId!, ct);
            if (application is not null)
            {
                var (identity, blocked, _, idClaimNames) = await BuildUserIdentityAsync(
                    application, user, principal.GetScopes(), sessionGuid?.ToString(), ct);

                // If the hook blocked — usually a transient hook outage under OnFailure=Block — don't
                // log the user out on a refresh: fall back to the last-known-good stored claims. The
                // hard gates (account status + session) above still apply; a role change simply waits
                // for the next successful refresh.
                if (!blocked)
                {
                    var refreshed = new ClaimsPrincipal(identity);
                    refreshed.SetScopes(principal.GetScopes());
                    identity.SetDestinations(claim => DestinationsFor(claim, idClaimNames));

                    // Slide the session so an active client keeps its login alive (mirrors refresh-token
                    // rotation). Revocation still wins — TouchSessionAsync no-ops on a revoked row.
                    if (sessionGuid is { } touch) await _auth.TouchSessionAsync(touch, ct);
                    return SignIn(refreshed, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                }
            }

            // App vanished, or the hook blocked: keep the session sliding and replay stored claims.
            if (sessionGuid is { } touchFallback) await _auth.TouchSessionAsync(touchFallback, ct);
        }

        return PassThrough(principal);
    }

    /// <summary>Re-signs the stored principal unchanged (auth-code exchange, or a refresh that falls
    /// back to last-known-good claims), routing each claim to its token per the granted scopes.</summary>
    private IActionResult PassThrough(ClaimsPrincipal principal)
    {
        foreach (var identity in principal.Identities)
            identity.SetDestinations(GetDestinations);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // --- UserInfo endpoint ---

    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [Produces("application/json")]
    public async Task<IActionResult> UserInfo()
    {
        var principal = (await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal!;

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = principal.GetClaim(Claims.Subject)!
        };

        if (principal.HasScope(Scopes.Email) && principal.GetClaim(Claims.Email) is { } email)
        {
            claims[Claims.Email] = email;
            claims[Claims.EmailVerified] = string.Equals(principal.GetClaim(Claims.EmailVerified), "true", StringComparison.OrdinalIgnoreCase);
        }

        if (principal.HasScope(Scopes.Profile) && principal.GetClaim(Claims.Name) is { } name)
            claims[Claims.Name] = name;

        if (principal.HasScope(Scopes.Profile) && principal.GetClaim(Claims.Picture) is { } picture)
            claims[Claims.Picture] = picture;

        if (principal.GetClaim(TenantClaim) is { } tenant)
            claims[TenantClaim] = tenant;

        return Ok(claims);
    }

    // --- End session (logout) ---

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.User);
        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }

    // --- helpers ---

    private const string TenantClaim = "tenant_id";
    private const string PermissionsClaim = "permissions";
    // Standard OIDC session-id claim. Binds a token to its login session so session revocation can be
    // enforced on refresh; also the claim RP-initiated / back-channel logout keys off.
    private const string SessionIdClaim = "sid";
    private const string TokenValidationParametersScheme = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme;

    private static AuthenticationProperties ErrorProps(string error, string description) => new(new Dictionary<string, string?>
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
    });

    private static string DisplayName(string? first, string? last, string fallbackEmail)
    {
        var name = $"{first} {last}".Trim();
        return string.IsNullOrEmpty(name) ? fallbackEmail : name;
    }

    // Standard/reserved claims are owned by §5.6 step 1 and must not be clobbered by custom claims.
    private static readonly HashSet<string> ReservedClaimNames = new(StringComparer.Ordinal)
    {
        Claims.Subject, Claims.Issuer, Claims.Audience, Claims.ExpiresAt, Claims.IssuedAt, Claims.NotBefore,
        Claims.Email, Claims.EmailVerified, Claims.Name, Claims.Role, TenantClaim, PermissionsClaim, SessionIdClaim
    };

    // The authorization claims a pre-token hook MAY override even though they're reserved: external
    // RBAC via hook (roles/permissions resolved by an upstream system) is a first-class, generic use
    // case. Identity/protocol claims (sub, email, name, tenant_id, …) stay hook-proof — a hook must
    // never be able to change who the token is for or which tenant it belongs to.
    private static readonly HashSet<string> HookOverridableClaims = new(StringComparer.Ordinal)
    {
        Claims.Role, PermissionsClaim
    };

    // A custom claim may be written when it isn't reserved at all; a reserved claim may be written
    // only when a pre-token hook produced it AND it's an authorization claim hooks are allowed to own.
    private static bool CanWriteCustomClaim(string name, IReadOnlySet<string>? hookClaimNames)
        => !ReservedClaimNames.Contains(name)
           || (hookClaimNames is { } h && h.Contains(name) && HookOverridableClaims.Contains(name));

    /// <summary>
    /// Builds the end-user token identity (§5.6): standard profile claims, the session-binding
    /// <c>sid</c> claim, RBAC roles/permissions, and tenant custom + pre-token-hook claims. Shared by
    /// the initial authorization and by refresh, so refresh re-derives roles/permissions/hook claims
    /// from current state instead of replaying the frozen ones captured at login.
    /// </summary>
    private async Task<(ClaimsIdentity Identity, bool Blocked, string? Reason, HashSet<string> IdClaimNames)>
        BuildUserIdentityAsync(Application application, User user, IEnumerable<string> scopes, string? sessionId, CancellationToken ct)
    {
        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParametersScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.EmailVerified, user.EmailVerified.ToString().ToLowerInvariant())
            .SetClaim(Claims.Name, DisplayName(user.FirstName, user.LastName, user.Email))
            // Standard OIDC profile claim; SetClaim no-ops on a null/empty avatar.
            .SetClaim(Claims.Picture, user.AvatarUrl)
            .SetClaim(TenantClaim, application.TenantId.ToString())
            // Session binding: SetClaim no-ops on a null/empty session id (logins with no session row).
            .SetClaim(SessionIdClaim, sessionId);

        // RBAC: inject the user's effective roles + flattened permissions (§5.6).
        var authorization = await _rbac.GetUserAuthorizationAsync(application.TenantId, user.Id, ct);
        identity.SetClaims(Claims.Role, authorization.Roles.ToImmutableArray());
        identity.SetClaims(PermissionsClaim, authorization.Permissions.ToImmutableArray());

        // §5.6 steps 2–4: tenant custom claims (static + metadata) and pre-token webhook claims.
        var payload = new
        {
            sub = user.Id.ToString(),
            email = user.Email,
            tenant_id = application.TenantId.ToString(),
            client_id = application.ClientId,
            scopes = scopes.ToArray()
        };
        var (blocked, reason, idClaimNames) = await ApplyCustomClaimsAsync(
            identity, application.TenantId, application.Id, user.UserMetadata, user.AppMetadata, payload, ct);

        return (identity, blocked, reason, idClaimNames);
    }

    /// <summary>
    /// Assembles tenant custom claims (§5.6 steps 2–4) and writes them onto the identity. Pre-token
    /// hooks run once (for the access pass); the id pass only adds static/metadata claims. Returns
    /// whether issuance was blocked and the set of claim names destined for the identity token.
    /// </summary>
    private async Task<(bool Blocked, string? Reason, HashSet<string> IdClaimNames)> ApplyCustomClaimsAsync(
        ClaimsIdentity identity, Guid tenantId, Guid applicationId,
        string? userMetadataJson, string? appMetadataJson, object payload, CancellationToken ct)
    {
        var idClaimNames = new HashSet<string>(StringComparer.Ordinal);

        var access = await _claims.AssembleAsync(new ClaimAssemblyRequest(
            tenantId, applicationId, ClaimTokenType.Access, userMetadataJson, appMetadataJson, payload), ct);
        if (access.Blocked)
            return (true, access.BlockReason, idClaimNames);

        // The id pass doesn't re-run the hook; pass the access pass's claims so a `Hook`-typed id
        // claim config can surface a hook value (e.g. company_id) in the id_token too.
        var id = await _claims.AssembleAsync(new ClaimAssemblyRequest(
            tenantId, applicationId, ClaimTokenType.Id, userMetadataJson, appMetadataJson, payload,
            RunPreTokenHooks: false, HookClaims: access.Claims), ct);

        foreach (var (name, value) in access.Claims)
            if (CanWriteCustomClaim(name, access.HookClaimNames)) identity.SetClaim(name, value);

        foreach (var (name, value) in id.Claims)
            if (CanWriteCustomClaim(name, id.HookClaimNames)) { identity.SetClaim(name, value); idClaimNames.Add(name); }

        return (false, null, idClaimNames);
    }

    private static IEnumerable<string> DestinationsFor(Claim claim, HashSet<string> idClaimNames)
    {
        if (idClaimNames.Contains(claim.Type))
        {
            yield return Destinations.AccessToken;
            yield return Destinations.IdentityToken;
            yield break;
        }
        foreach (var destination in GetDestinations(claim))
            yield return destination;
    }

    /// <summary>Routes each claim to the access token and/or identity token per the granted scopes.</summary>
    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        switch (claim.Type)
        {
            case Claims.Name:
                yield return Destinations.AccessToken;
                if (claim.Subject!.HasScope(Scopes.Profile))
                    yield return Destinations.IdentityToken;
                yield break;

            // Avatar is display-only identity data: id_token only (under the profile
            // scope), never the access token — APIs don't authorize on it.
            case Claims.Picture:
                if (claim.Subject!.HasScope(Scopes.Profile))
                    yield return Destinations.IdentityToken;
                yield break;

            case Claims.Email:
            case Claims.EmailVerified:
                yield return Destinations.AccessToken;
                if (claim.Subject!.HasScope(Scopes.Email))
                    yield return Destinations.IdentityToken;
                yield break;

            case TenantClaim:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            // Session id: needed in both tokens — the access token so it survives the refresh round-trip
            // (session-revocation enforcement reads it back), the id token per standard OIDC logout.
            case SessionIdClaim:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            // Roles + permissions are authorization data: always in the access token; also in the
            // identity token when the "roles" scope was granted.
            case Claims.Role:
            case PermissionsClaim:
                yield return Destinations.AccessToken;
                if (claim.Subject!.HasScope(Scopes.Roles))
                    yield return Destinations.IdentityToken;
                yield break;

            // Never leak the security stamp / internal claims.
            case "AspNet.Identity.SecurityStamp":
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}
