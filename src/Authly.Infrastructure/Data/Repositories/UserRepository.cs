using Authly.Core.Common;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == id, ct);

    public Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email, ct);

    public Task<User?> GetByVerifiedPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.PhoneVerified && u.Phone == normalizedPhone, ct);

    public Task<User?> GetByPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Phone == normalizedPhone, ct);

    public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken ct = default)
        => _db.Users.AnyAsync(u => u.TenantId == tenantId && u.Email == email, ct);

    public async Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Users
            .Where(u => u.TenantId == tenantId)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(ct);

    public async Task<PagedResult<User>> ListPagedAsync(Guid tenantId, Pagination page, string? emailContains = null, CancellationToken ct = default)
    {
        var query = _db.Users.Where(u => u.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(emailContains))
            query = query.Where(u => u.Email.Contains(emailContains));

        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip(page.Skip).Take(page.Limit)
            .ToListAsync(ct);

        return new PagedResult<User>(items, total);
    }

    public async Task DeleteAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync(ct);
    }
}
