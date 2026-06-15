using System.Text.Json;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.OAuth;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Authly.Modules.Social;

/// <inheritdoc />
public sealed class SocialLoginService : ISocialLoginService
{
    private readonly ISocialProviderRepository _providers;
    private readonly ISocialIdentityRepository _identities;
    private readonly IUserRepository _users;
    private readonly IAuthService _auth;
    private readonly ISocialAuthGateway _gateway;
    private readonly IEncryptionService _encryption;
    private readonly IAuditLogger _audit;
    private readonly ILogger<SocialLoginService> _logger;

    public SocialLoginService(
        ISocialProviderRepository providers,
        ISocialIdentityRepository identities,
        IUserRepository users,
        IAuthService auth,
        ISocialAuthGateway gateway,
        IEncryptionService encryption,
        IAuditLogger audit,
        ILogger<SocialLoginService> logger)
    {
        _providers = providers;
        _identities = identities;
        _users = users;
        _auth = auth;
        _gateway = gateway;
        _encryption = encryption;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SocialLoginOption>> ListActiveOptionsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var configured = await _providers.ListByTenantAsync(tenantId, ct);
        return configured
            .Where(p => p.IsActive)
            .Select(p => new SocialLoginOption(p.Provider, SocialProviderPresets.Find(p.Provider)?.DisplayName ?? p.Provider))
            .ToList();
    }

    public async Task<string> BuildAuthorizationUrlAsync(Guid tenantId, string provider, string redirectUri, string state, CancellationToken ct = default)
    {
        var (config, _) = await ResolveConfigAsync(tenantId, provider, ct);
        return _gateway.BuildAuthorizationUrl(config, redirectUri, state);
    }

    public async Task<SocialLoginResult> CompleteLoginAsync(Guid tenantId, string provider, string code, string redirectUri, RequestInfo info, CancellationToken ct = default)
    {
        var (config, preset) = await ResolveConfigAsync(tenantId, provider, ct);

        var tokens = await _gateway.ExchangeCodeAsync(config, code, redirectUri, ct);
        var json = await _gateway.FetchUserInfoJsonAsync(config, tokens, ct);
        var profile = ParseProfile(json, preset);

        if (string.IsNullOrWhiteSpace(profile.ProviderId))
            throw new SocialProviderConfigInvalidException($"The '{provider}' profile had no id field.");

        var existing = await _identities.GetAsync(tenantId, provider, profile.ProviderId, ct);

        User user;
        var isNew = false;
        var linked = false;

        if (existing is not null)
        {
            user = await _users.GetByIdAsync(tenantId, existing.UserId, ct)
                ?? throw new SocialProviderConfigInvalidException("Linked user no longer exists.");
            ApplyTokens(existing, profile, tokens, json);
            await _identities.UpdateAsync(existing, ct);
        }
        else
        {
            var email = string.IsNullOrWhiteSpace(profile.Email) ? null : profile.Email.Trim().ToLowerInvariant();

            // Only link/create from a provider-verified email — prevents account takeover via an
            // attacker-controlled unverified address at the provider.
            if (email is null || !profile.EmailVerified)
                throw new SocialProfileMissingEmailException(provider);

            var match = await _users.GetByEmailAsync(tenantId, email, ct);
            if (match is not null)
            {
                user = match;
                linked = true;
            }
            else
            {
                user = new User
                {
                    TenantId = tenantId,
                    Email = email,
                    EmailVerified = true,            // verified by the provider
                    PasswordHash = null,             // social-only account
                    Status = UserStatus.Active,
                    FirstName = profile.Name,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                await _users.AddAsync(user, ct);
                isNew = true;
            }

            var identity = new SocialIdentity
            {
                UserId = user.Id,
                TenantId = tenantId,
                Provider = provider,
                ProviderId = profile.ProviderId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            ApplyTokens(identity, profile, tokens, json);
            await _identities.AddAsync(identity, ct);
        }

        var session = await _auth.StartSessionAsync(user, provider, info, ct);

        await _audit.LogAsync("user.social_login", new AuditContext(user.Id, "user", info.IpAddress, info.UserAgent),
            tenantId, "user", user.Id, metadata: new { provider, isNew, linked }, ct: ct);

        return new SocialLoginResult(user, session, isNew, linked);
    }

    // --- Admin --------------------------------------------------------------

    public Task<IReadOnlyList<SocialProvider>> ListProvidersAsync(Guid tenantId, CancellationToken ct = default)
        => _providers.ListByTenantAsync(tenantId, ct);

    public Task<SocialProvider?> GetProviderAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _providers.GetByIdAsync(tenantId, id, ct);

    public async Task SaveProviderAsync(Guid tenantId, SocialProviderInput input, AuditContext actor, CancellationToken ct = default)
    {
        Validate(input);

        var existing = input.Id is { } id
            ? await _providers.GetByIdAsync(tenantId, id, ct)
            : await _providers.GetAsync(tenantId, input.Provider, ct);

        // Secret: encrypt a new value, else keep the already-encrypted stored one.
        var secret = string.IsNullOrWhiteSpace(input.ClientSecret)
            ? existing?.ClientSecret
            : _encryption.Encrypt(input.ClientSecret);

        if (existing is null)
        {
            await _providers.AddAsync(new SocialProvider
            {
                TenantId = tenantId,
                Provider = input.Provider,
                ClientId = input.ClientId,
                ClientSecret = secret,
                Scopes = input.Scopes,
                AuthorizationEndpoint = input.AuthorizationEndpoint,
                TokenEndpoint = input.TokenEndpoint,
                UserInfoEndpoint = input.UserInfoEndpoint,
                IsActive = input.IsActive,
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);
        }
        else
        {
            existing.Provider = input.Provider;
            existing.ClientId = input.ClientId;
            existing.ClientSecret = secret;
            existing.Scopes = input.Scopes;
            existing.AuthorizationEndpoint = input.AuthorizationEndpoint;
            existing.TokenEndpoint = input.TokenEndpoint;
            existing.UserInfoEndpoint = input.UserInfoEndpoint;
            existing.IsActive = input.IsActive;
            await _providers.UpdateAsync(existing, ct);
        }

        await _audit.LogAsync("social.provider_saved", actor, tenantId, "social_provider", existing?.Id,
            metadata: new { input.Provider, input.IsActive }, ct: ct);
    }

    public async Task DeleteProviderAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var entity = await _providers.GetByIdAsync(tenantId, id, ct);
        if (entity is null) return;
        await _providers.DeleteAsync(entity, ct);
        await _audit.LogAsync("social.provider_deleted", actor, tenantId, "social_provider", id, ct: ct);
    }

    // --- helpers ------------------------------------------------------------

    private async Task<(SocialAuthConfig Config, SocialProviderPreset? Preset)> ResolveConfigAsync(Guid tenantId, string provider, CancellationToken ct)
    {
        var entity = await _providers.GetAsync(tenantId, provider, ct);
        if (entity is null || !entity.IsActive)
            throw new SocialProviderNotConfiguredException(provider);

        var preset = SocialProviderPresets.Find(provider);
        var auth = entity.AuthorizationEndpoint ?? preset?.AuthorizationEndpoint;
        var token = entity.TokenEndpoint ?? preset?.TokenEndpoint;
        var userInfo = entity.UserInfoEndpoint ?? preset?.UserInfoEndpoint;

        if (auth is null || token is null || userInfo is null)
            throw new SocialProviderConfigInvalidException($"Provider '{provider}' is missing OAuth endpoints.");

        var scopes = !string.IsNullOrWhiteSpace(entity.Scopes)
            ? entity.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : preset?.DefaultScopes.ToArray() ?? Array.Empty<string>();

        var secret = string.IsNullOrEmpty(entity.ClientSecret) ? "" : _encryption.Decrypt(entity.ClientSecret);

        return (new SocialAuthConfig(provider, entity.ClientId, secret, scopes, auth, token, userInfo), preset);
    }

    private void ApplyTokens(SocialIdentity identity, SocialProfile profile, SocialTokenSet tokens, string rawJson)
    {
        identity.ProviderEmail = profile.Email;
        identity.AccessToken = string.IsNullOrEmpty(tokens.AccessToken) ? null : _encryption.Encrypt(tokens.AccessToken);
        identity.RefreshToken = string.IsNullOrEmpty(tokens.RefreshToken) ? null : _encryption.Encrypt(tokens.RefreshToken!);
        identity.ExpiresAt = tokens.ExpiresAt;
        identity.RawProfile = rawJson;
    }

    private static SocialProfile ParseProfile(string json, SocialProviderPreset? preset)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var idField = preset?.IdField ?? (root.TryGetProperty("sub", out _) ? "sub" : "id");
        var emailField = preset?.EmailField ?? "email";
        var nameField = preset?.NameField ?? "name";
        var verifiedField = preset?.EmailVerifiedField; // null → trust the provider's email

        var providerId = ReadScalar(root, idField);
        var email = ReadScalar(root, emailField);
        var name = ReadScalar(root, nameField);

        bool emailVerified;
        if (verifiedField is not null && root.TryGetProperty(verifiedField, out var v))
            emailVerified = v.ValueKind == JsonValueKind.True
                || (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b) && b);
        else
            emailVerified = email is not null; // curated presets return verified emails

        return new SocialProfile(providerId ?? "", email, emailVerified, name);
    }

    private static string? ReadScalar(JsonElement root, string field)
    {
        if (!root.TryGetProperty(field, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null
        };
    }

    private static void Validate(SocialProviderInput input)
    {
        if (string.IsNullOrWhiteSpace(input.ClientId))
            throw new SocialProviderConfigInvalidException("Client id is required.");

        if (!SocialProviderPresets.IsKnown(input.Provider))
        {
            // A custom/generic provider must supply its own endpoints.
            if (string.IsNullOrWhiteSpace(input.AuthorizationEndpoint)
                || string.IsNullOrWhiteSpace(input.TokenEndpoint)
                || string.IsNullOrWhiteSpace(input.UserInfoEndpoint))
                throw new SocialProviderConfigInvalidException("Custom providers require authorization, token, and user-info endpoints.");
        }
    }

    private sealed record SocialProfile(string ProviderId, string? Email, bool EmailVerified, string? Name);
}
