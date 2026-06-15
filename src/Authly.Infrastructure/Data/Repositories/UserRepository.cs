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

    public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken ct = default)
        => _db.Users.AnyAsync(u => u.TenantId == tenantId && u.Email == email, ct);

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
