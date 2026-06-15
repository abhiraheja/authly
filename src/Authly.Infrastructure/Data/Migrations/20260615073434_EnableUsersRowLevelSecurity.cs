using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authly.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnableUsersRowLevelSecurity : Migration
    {
        // Row Level Security backstop on the canonical tenant-scoped table. The app ALSO
        // filters by tenant_id in every query (primary guarantee); this enforces isolation
        // even if a query forgets to. FORCE makes it apply to the table owner too. The
        // session variable app.current_tenant is set per connection by TenantConnectionInterceptor;
        // NULLIF('', ...) treats "no tenant set" as deny-all.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE users ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE users FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
                CREATE POLICY tenant_isolation ON users
                    USING (tenant_id = NULLIF(current_setting('app.current_tenant', true), '')::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON users;");
            migrationBuilder.Sql("ALTER TABLE users NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE users DISABLE ROW LEVEL SECURITY;");
        }
    }
}
