using Microsoft.EntityFrameworkCore;
using Authly.Core.Entities;
using Authly.Core.Enums;

namespace Authly.Infrastructure.Data;

/// <summary>
/// Primary EF Core context. Column/table names match the PostgreSQL schema in the
/// technical handoff (snake_case). Tenant-scoped tables also get Row Level Security
/// applied via raw SQL migrations as a backstop (Phase 1).
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Slug).HasColumnName("slug").IsRequired();
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>()
                .HasDefaultValue(TenantStatus.Active).IsRequired();
            e.Property(x => x.ParentId).HasColumnName("parent_id");
            e.Property(x => x.Settings).HasColumnName("settings").HasColumnType("jsonb").HasDefaultValueSql("'{}'");
            e.Property(x => x.Branding).HasColumnName("branding").HasColumnType("jsonb").HasDefaultValueSql("'{}'");
            e.Property(x => x.CustomDomain).HasColumnName("custom_domain");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("idx_tenants_slug");
            e.HasIndex(x => x.ParentId).HasDatabaseName("idx_tenants_parent");

            e.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Email).HasColumnName("email").IsRequired();
            e.Property(x => x.EmailVerified).HasColumnName("email_verified").HasDefaultValue(false);
            e.Property(x => x.Username).HasColumnName("username");
            e.Property(x => x.Phone).HasColumnName("phone");
            e.Property(x => x.PhoneVerified).HasColumnName("phone_verified").HasDefaultValue(false);
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>()
                .HasDefaultValue(UserStatus.Active).IsRequired();
            e.Property(x => x.IsAnonymous).HasColumnName("is_anonymous").HasDefaultValue(false);
            e.Property(x => x.FirstName).HasColumnName("first_name");
            e.Property(x => x.LastName).HasColumnName("last_name");
            e.Property(x => x.AvatarUrl).HasColumnName("avatar_url");
            e.Property(x => x.Timezone).HasColumnName("timezone").HasDefaultValue("UTC");
            e.Property(x => x.Locale).HasColumnName("locale").HasDefaultValue("en");
            e.Property(x => x.UserMetadata).HasColumnName("user_metadata").HasColumnType("jsonb").HasDefaultValueSql("'{}'");
            e.Property(x => x.AppMetadata).HasColumnName("app_metadata").HasColumnType("jsonb").HasDefaultValueSql("'{}'");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            e.Property(x => x.LastLoginAt).HasColumnName("last_login_at");

            // Email is unique PER tenant, not globally.
            e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique().HasDatabaseName("idx_users_email");
            e.HasIndex(x => x.TenantId).HasDatabaseName("idx_users_tenant");

            e.HasOne(x => x.Tenant).WithMany(t => t.Users).HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
