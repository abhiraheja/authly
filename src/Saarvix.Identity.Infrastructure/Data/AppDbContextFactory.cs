using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Saarvix.Identity.Infrastructure.Data;

/// <summary>
/// Design-time factory used by `dotnet ef` for migrations. Uses DATABASE_URL when
/// present, otherwise a localhost default so migrations can be scaffolded offline.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("DATABASE_URL")
                   ?? "Host=localhost;Database=saarvix;Username=saarvix;Password=saarvix";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new AppDbContext(options);
    }
}
