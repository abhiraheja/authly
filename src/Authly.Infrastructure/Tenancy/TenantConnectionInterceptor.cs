using System.Data.Common;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Authly.Infrastructure.Tenancy;

/// <summary>
/// Sets the PostgreSQL session variable <c>app.current_tenant</c> on every opened
/// connection, from the current request's <see cref="ITenantContext"/>. The RLS policy on
/// tenant-scoped tables reads this variable, so even a query that forgets to filter by
/// tenant_id cannot cross tenants. When no tenant is in scope the variable is cleared,
/// so a pooled connection never leaks a previous request's tenant.
/// </summary>
public sealed class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenant;

    public TenantConnectionInterceptor(ITenantContext tenant) => _tenant = tenant;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Apply(connection);

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken ct = default)
        => await ApplyAsync(connection, ct);

    private void Apply(DbConnection connection)
    {
        using var cmd = CreateSetCommand(connection);
        cmd.ExecuteNonQuery();
    }

    private async Task ApplyAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = CreateSetCommand(connection);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private DbCommand CreateSetCommand(DbConnection connection)
    {
        var cmd = connection.CreateCommand();
        // Empty string when no tenant; the policy treats '' as "no tenant" (deny all).
        cmd.CommandText = "SELECT set_config('app.current_tenant', @tenant, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "tenant";
        p.Value = _tenant.HasTenant ? _tenant.TenantId!.Value.ToString() : string.Empty;
        cmd.Parameters.Add(p);
        return cmd;
    }
}
