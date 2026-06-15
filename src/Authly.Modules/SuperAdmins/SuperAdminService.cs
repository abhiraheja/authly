using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Authly.Modules.SuperAdmins;

/// <inheritdoc />
public sealed class SuperAdminService : ISuperAdminService
{
    private readonly ISuperAdminRepository _repo;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<SuperAdminService> _logger;

    public SuperAdminService(ISuperAdminRepository repo, IPasswordHasher hasher, ILogger<SuperAdminService> logger)
    {
        _repo = repo;
        _hasher = hasher;
        _logger = logger;
    }

    public Task<SuperAdmin?> GetAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);

    public async Task<SuperAdmin?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default)
    {
        var admin = await _repo.GetByEmailAsync(email.Trim().ToLowerInvariant(), ct);
        if (admin is null) return null;
        return _hasher.Verify(admin.PasswordHash, password) ? admin : null;
    }

    public async Task RecordLoginAsync(Guid id, CancellationToken ct = default)
    {
        var admin = await _repo.GetByIdAsync(id, ct);
        if (admin is null) return;
        admin.LastLoginAt = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(admin, ct);
    }

    public async Task ChangePasswordAsync(Guid id, string newPassword, CancellationToken ct = default)
    {
        var admin = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Super admin {id} not found.");
        admin.PasswordHash = _hasher.Hash(newPassword);
        admin.MustChangePassword = false;
        await _repo.UpdateAsync(admin, ct);
    }

    public async Task EnsureSeededAsync(string? email, string? password, CancellationToken ct = default)
    {
        if (await _repo.AnyAsync(ct))
            return; // already bootstrapped

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("No super admin exists and SUPERADMIN_EMAIL/PASSWORD are not set. " +
                                "Set them to bootstrap the platform owner.");
            return;
        }

        var admin = new SuperAdmin
        {
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = _hasher.Hash(password),
            Role = SuperAdminRole.Owner,
            MfaEnabled = true,
            MustChangePassword = true, // force change of the seeded password on first login
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repo.AddAsync(admin, ct);
        _logger.LogInformation("Bootstrapped initial super admin {Email} (Owner). Password change is required on first login.", admin.Email);
    }
}
