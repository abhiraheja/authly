using System.Text.Json;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Users;

/// <summary>Recognized export formats for the user importer.</summary>
public enum ImportSource { Generic, Auth0, Firebase }

/// <summary>A normalized user from an import file (passwords are never imported — see service notes).</summary>
public sealed record ImportedUser(string Email, string? FirstName, string? LastName, bool EmailVerified);

/// <summary>Summary of an import run.</summary>
public sealed record ImportResult(int Created, int Skipped, IReadOnlyList<string> Errors)
{
    public int Total => Created + Skipped + Errors.Count;
}

/// <summary>
/// Bulk-imports users from an Auth0 / Firebase / generic JSON export. Passwords are intentionally
/// NOT migrated (foreign hash formats aren't verifiable here) — imported users are created without
/// a password and must use "forgot password" to set one, so no credential is ever weakened.
/// Tenant-scoped; reuses <see cref="IUserAdminService.CreateAsync"/> for creation + per-user audit.
/// </summary>
public interface IUserImportService
{
    Task<ImportResult> ImportAsync(Guid tenantId, ImportSource source, string json, AuditContext actor, CancellationToken ct = default);
}

/// <summary>Pure normalization of the supported export shapes into <see cref="ImportedUser"/> rows.</summary>
public static class UserImportParser
{
    public static IReadOnlyList<ImportedUser> Parse(ImportSource source, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Firebase wraps users in { "users": [...] }; the others are a bare array.
        var array = source == ImportSource.Firebase && root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("users", out var u)
            ? u
            : root;
        if (array.ValueKind != JsonValueKind.Array)
            return Array.Empty<ImportedUser>();

        var list = new List<ImportedUser>();
        foreach (var el in array.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            var parsed = source switch
            {
                ImportSource.Auth0 => ParseAuth0(el),
                ImportSource.Firebase => ParseFirebase(el),
                _ => ParseGeneric(el)
            };
            if (parsed is not null) list.Add(parsed);
        }
        return list;
    }

    private static ImportedUser? ParseGeneric(JsonElement el)
    {
        var email = Str(el, "email");
        return email is null ? null
            : new ImportedUser(email, Str(el, "firstName"), Str(el, "lastName"), Bool(el, "emailVerified"));
    }

    private static ImportedUser? ParseAuth0(JsonElement el)
    {
        var email = Str(el, "email");
        if (email is null) return null;
        var first = Str(el, "given_name");
        var last = Str(el, "family_name");
        if (first is null && last is null) (first, last) = SplitName(Str(el, "name"));
        return new ImportedUser(email, first, last, Bool(el, "email_verified"));
    }

    private static ImportedUser? ParseFirebase(JsonElement el)
    {
        var email = Str(el, "email");
        if (email is null) return null;
        var (first, last) = SplitName(Str(el, "displayName"));
        return new ImportedUser(email, first, last, Bool(el, "emailVerified"));
    }

    private static (string? First, string? Last) SplitName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return (null, null);
        var parts = name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 1 ? (parts[0], null) : (parts[0], parts[1]);
    }

    private static string? Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? (string.IsNullOrWhiteSpace(v.GetString()) ? null : v.GetString()!.Trim())
            : null;

    private static bool Bool(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}

/// <inheritdoc />
public sealed class UserImportService : IUserImportService
{
    private readonly IUserAdminService _users;
    private readonly IAuditLogger _audit;

    public UserImportService(IUserAdminService users, IAuditLogger audit)
    {
        _users = users;
        _audit = audit;
    }

    public async Task<ImportResult> ImportAsync(Guid tenantId, ImportSource source, string json, AuditContext actor, CancellationToken ct = default)
    {
        List<ImportedUser> rows;
        try
        {
            rows = UserImportParser.Parse(source, json).ToList();
        }
        catch (JsonException ex)
        {
            return new ImportResult(0, 0, new[] { $"Invalid JSON: {ex.Message}" });
        }

        int created = 0, skipped = 0;
        var errors = new List<string>();
        foreach (var row in rows)
        {
            try
            {
                await _users.CreateAsync(tenantId,
                    new CreateUserRequest(row.Email, Password: null, row.FirstName, row.LastName, row.EmailVerified),
                    actor, ct);
                created++;
            }
            catch (UserEmailAlreadyExistsException)
            {
                skipped++; // already exists — leave the existing account untouched.
            }
            catch (Exception ex)
            {
                errors.Add($"{row.Email}: {ex.Message}");
            }
        }

        await _audit.LogAsync("users.imported", actor, tenantId, "tenant", tenantId,
            metadata: new { source = source.ToString(), created, skipped, errors = errors.Count }, ct: ct);

        return new ImportResult(created, skipped, errors);
    }
}
