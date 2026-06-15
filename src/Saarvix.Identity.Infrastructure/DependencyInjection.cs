using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Saarvix.Identity.Core.Interfaces;
using Saarvix.Identity.Infrastructure.Data;
using Saarvix.Identity.Infrastructure.Security;
using StackExchange.Redis;

namespace Saarvix.Identity.Infrastructure;

/// <summary>Composition-root registration for the Infrastructure layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // --- PostgreSQL / EF Core ---
        var databaseUrl = config["DATABASE_URL"]
            ?? "Host=localhost;Database=saarvix;Username=saarvix;Password=saarvix";
        services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(databaseUrl));

        // --- Redis (cache, sessions, rate limits) ---
        var redisUrl = config["REDIS_URL"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisUrl));
        services.AddStackExchangeRedisCache(o => o.Configuration = redisUrl);

        // --- Security primitives ---
        services.Configure<EncryptionOptions>(o => o.Key = config["ENCRYPTION_KEY"]!);
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<IEncryptionService, AesEncryptionService>();

        return services;
    }
}
