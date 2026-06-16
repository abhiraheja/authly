using Authly.Core.Common;
using Authly.Core.Entities;
using Authly.Modules.Common;
using Authly.Modules.Users;

namespace Authly.Tests.Phase2;

public class UserImportTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    // --- Parser ---

    [Fact]
    public void Generic_array_parses_fields()
    {
        var rows = UserImportParser.Parse(ImportSource.Generic,
            """[{"email":"a@x.com","firstName":"Ada","lastName":"Lovelace","emailVerified":true}]""");
        var u = Assert.Single(rows);
        Assert.Equal("a@x.com", u.Email);
        Assert.Equal("Ada", u.FirstName);
        Assert.Equal("Lovelace", u.LastName);
        Assert.True(u.EmailVerified);
    }

    [Fact]
    public void Auth0_uses_given_family_then_falls_back_to_name_split()
    {
        var rows = UserImportParser.Parse(ImportSource.Auth0,
            """[{"email":"a@x.com","given_name":"Ada","family_name":"L","email_verified":true},{"email":"b@x.com","name":"Grace Hopper"}]""");
        Assert.Equal(2, rows.Count);
        Assert.Equal("Ada", rows[0].FirstName);
        Assert.Equal("L", rows[0].LastName);
        Assert.True(rows[0].EmailVerified);
        Assert.Equal("Grace", rows[1].FirstName);
        Assert.Equal("Hopper", rows[1].LastName);
        Assert.False(rows[1].EmailVerified);
    }

    [Fact]
    public void Firebase_unwraps_users_and_splits_displayName()
    {
        var rows = UserImportParser.Parse(ImportSource.Firebase,
            """{"users":[{"email":"a@x.com","displayName":"Ada Lovelace","emailVerified":true}]}""");
        var u = Assert.Single(rows);
        Assert.Equal("Ada", u.FirstName);
        Assert.Equal("Lovelace", u.LastName);
    }

    [Fact]
    public void Rows_without_email_are_skipped()
    {
        var rows = UserImportParser.Parse(ImportSource.Generic,
            """[{"firstName":"NoEmail"},{"email":"  "},{"email":"ok@x.com"}]""");
        var u = Assert.Single(rows);
        Assert.Equal("ok@x.com", u.Email);
    }

    // --- Service ---

    [Fact]
    public async Task Import_creates_new_skips_duplicates_and_collects_errors()
    {
        var admin = new FakeUserAdmin(existing: new() { "dup@x.com" }, failOn: "boom@x.com");
        var audit = new ImpersonationRecordingAudit();
        var svc = new UserImportService(admin, audit);

        var json = """[{"email":"new@x.com"},{"email":"dup@x.com"},{"email":"boom@x.com"}]""";
        var result = await svc.ImportAsync(Tenant, ImportSource.Generic, json, AuditContext.System);

        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Skipped);
        Assert.Single(result.Errors);
        Assert.Equal(3, result.Total);
        Assert.Contains("users.imported", audit.Events);
        Assert.Contains("new@x.com", admin.Created);
    }

    [Fact]
    public async Task Invalid_json_returns_an_error_without_throwing()
    {
        var svc = new UserImportService(new FakeUserAdmin(new(), null), new ImpersonationRecordingAudit());
        var result = await svc.ImportAsync(Tenant, ImportSource.Generic, "not json", AuditContext.System);
        Assert.Equal(0, result.Created);
        Assert.Single(result.Errors);
    }
}

internal sealed class FakeUserAdmin : IUserAdminService
{
    private readonly HashSet<string> _existing;
    private readonly string? _failOn;
    public readonly List<string> Created = new();

    public FakeUserAdmin(HashSet<string> existing, string? failOn)
    {
        _existing = existing;
        _failOn = failOn;
    }

    public Task<User> CreateAsync(Guid tenantId, CreateUserRequest request, AuditContext actor, CancellationToken ct = default)
    {
        if (string.Equals(request.Email, _failOn, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("boom");
        if (!_existing.Add(request.Email))
            throw new UserEmailAlreadyExistsException(request.Email);
        Created.Add(request.Email);
        return Task.FromResult(new User { Id = Guid.NewGuid(), TenantId = tenantId, Email = request.Email });
    }

    // Unused by these tests.
    public Task<PagedResult<User>> ListAsync(Guid tenantId, Pagination page, string? emailContains, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<User?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<User> UpdateAsync(Guid tenantId, Guid id, UpdateUserRequest request, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SuspendAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task ReactivateAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task ForcePasswordResetAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Session>> ListSessionsAsync(Guid tenantId, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<int> RevokeAllSessionsAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
}
