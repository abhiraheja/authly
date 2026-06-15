using Authly.Core.Entities;

namespace Authly.Modules.SuperAdmins;

/// <summary>Authentication and account operations for platform super admins.</summary>
public interface ISuperAdminService
{
    Task<SuperAdmin?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the super admin when email+password match, otherwise null.</summary>
    Task<SuperAdmin?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);

    Task RecordLoginAsync(Guid id, CancellationToken ct = default);

    /// <summary>Sets a new password and clears the must-change flag.</summary>
    Task ChangePasswordAsync(Guid id, string newPassword, CancellationToken ct = default);

    /// <summary>Seeds the first super admin from configuration if none exist yet.</summary>
    Task EnsureSeededAsync(string? email, string? password, CancellationToken ct = default);
}
