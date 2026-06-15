using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Authly.Infrastructure.Data;

/// <summary>
/// Design-time factory used by `dotnet ef` for migrations. Uses DATABASE_URL when
/// present, otherwise a localhost default so migrations can be scaffolded offline.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("DATABASE_URL")
                   ?? "Host=localhost;Database=authly;Username=authly;Password=authly";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .UseOpenIddict() // register OpenIddict's EF entities so migrations include them
            .Options;

        return new AppDbContext(options);
    }
}
