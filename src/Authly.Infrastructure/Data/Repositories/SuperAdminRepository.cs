using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class SuperAdminRepository : ISuperAdminRepository
{
    private readonly AppDbContext _db;

    public SuperAdminRepository(AppDbContext db) => _db = db;

    public Task<SuperAdmin?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.SuperAdmins.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<SuperAdmin?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _db.SuperAdmins.FirstOrDefaultAsync(a => a.Email == email, ct);

    public Task<bool> AnyAsync(CancellationToken ct = default)
        => _db.SuperAdmins.AnyAsync(ct);

    public async Task AddAsync(SuperAdmin admin, CancellationToken ct = default)
    {
        _db.SuperAdmins.Add(admin);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SuperAdmin admin, CancellationToken ct = default)
    {
        _db.SuperAdmins.Update(admin);
        await _db.SaveChangesAsync(ct);
    }
}
