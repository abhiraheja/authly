using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Authly.Core.Interfaces;
using Authly.Infrastructure.Data;
using Authly.Infrastructure.Data.Repositories;
using Authly.Infrastructure.Security;
using Authly.Infrastructure.Tenancy;
using StackExchange.Redis;

namespace Authly.Infrastructure;

/// <summary>Composition-root registration for the Infrastructure layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // --- PostgreSQL / EF Core ---
        var databaseUrl = config["DATABASE_URL"]
            ?? "Host=localhost;Database=authly;Username=authly;Password=authly";
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddDbContext<AppDbContext>((sp, opt) => opt
            .UseNpgsql(databaseUrl)
            .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));

        // --- Redis (cache, sessions, rate limits) ---
        var redisUrl = config["REDIS_URL"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisUrl));
        services.AddStackExchangeRedisCache(o => o.Configuration = redisUrl);

        // --- Security primitives ---
        services.Configure<EncryptionOptions>(o => o.Key = config["ENCRYPTION_KEY"]!);
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<IEncryptionService, AesEncryptionService>();

        // --- Tenancy ---
        services.AddScoped<ITenantContext, TenantContext>();

        // --- Repositories ---
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ISuperAdminRepository, SuperAdminRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        return services;
    }
}
