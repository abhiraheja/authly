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
    public DbSet<SuperAdmin> SuperAdmins => Set<SuperAdmin>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();
    public DbSet<VerificationToken> VerificationTokens => Set<VerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ApplicationSecret> ApplicationSecrets => Set<ApplicationSecret>();

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
            e.Property(x => x.IsTenantAdmin).HasColumnName("is_tenant_admin").HasDefaultValue(false);
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

        b.Entity<SuperAdmin>(e =>
        {
            e.ToTable("super_admins");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Email).HasColumnName("email").IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
            e.Property(x => x.Role).HasColumnName("role").HasConversion<string>()
                .HasDefaultValue(SuperAdminRole.Operator).IsRequired();
            e.Property(x => x.MfaEnabled).HasColumnName("mfa_enabled").HasDefaultValue(true);
            e.Property(x => x.MustChangePassword).HasColumnName("must_change_password").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.Property(x => x.LastLoginAt).HasColumnName("last_login_at");

            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("idx_super_admins_email");
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.ActorId).HasColumnName("actor_id");
            e.Property(x => x.ActorType).HasColumnName("actor_type");
            e.Property(x => x.Event).HasColumnName("event").IsRequired();
            e.Property(x => x.ResourceType).HasColumnName("resource_type");
            e.Property(x => x.ResourceId).HasColumnName("resource_id");
            e.Property(x => x.IpAddress).HasColumnName("ip_address");
            e.Property(x => x.UserAgent).HasColumnName("user_agent");
            e.Property(x => x.Result).HasColumnName("result").HasDefaultValue("success").IsRequired();
            e.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb").HasDefaultValueSql("'{}'");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.CreatedAt }).HasDatabaseName("idx_audit_tenant");
            e.HasIndex(x => new { x.ActorId, x.CreatedAt }).HasDatabaseName("idx_audit_actor");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Session>(e =>
        {
            e.ToTable("sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.ApplicationId).HasColumnName("application_id");
            e.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").IsRequired();
            e.Property(x => x.RefreshFamilyId).HasColumnName("refresh_family_id").IsRequired();
            e.Property(x => x.IpAddress).HasColumnName("ip_address");
            e.Property(x => x.UserAgent).HasColumnName("user_agent");
            e.Property(x => x.DeviceFingerprint).HasColumnName("device_fingerprint");
            e.Property(x => x.Location).HasColumnName("location");
            e.Property(x => x.Trusted).HasColumnName("trusted").HasDefaultValue(false);
            e.Property(x => x.LastActiveAt).HasColumnName("last_active_at").HasDefaultValueSql("NOW()");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            e.Property(x => x.Revoked).HasColumnName("revoked").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => x.RefreshTokenHash).IsUnique().HasDatabaseName("idx_sessions_refresh");
            e.HasIndex(x => new { x.UserId, x.TenantId }).HasDatabaseName("idx_sessions_user");
            e.HasIndex(x => x.RefreshFamilyId).HasDatabaseName("idx_sessions_family");

            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LoginHistory>(e =>
        {
            e.ToTable("login_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Result).HasColumnName("result").IsRequired();
            e.Property(x => x.Method).HasColumnName("method");
            e.Property(x => x.IpAddress).HasColumnName("ip_address");
            e.Property(x => x.UserAgent).HasColumnName("user_agent");
            e.Property(x => x.Device).HasColumnName("device");
            e.Property(x => x.Location).HasColumnName("location");
            e.Property(x => x.Reason).HasColumnName("reason");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("idx_login_user");
            e.HasIndex(x => new { x.TenantId, x.CreatedAt }).HasDatabaseName("idx_login_tenant");

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<VerificationToken>(e =>
        {
            e.ToTable("verification_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.Type).HasColumnName("type").IsRequired();
            e.Property(x => x.Target).HasColumnName("target").IsRequired();
            e.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            e.Property(x => x.Used).HasColumnName("used").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("idx_verification_token_hash");
            e.HasIndex(x => x.UserId).HasDatabaseName("idx_verification_user");

            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PasswordResetToken>(e =>
        {
            e.ToTable("password_reset_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            e.Property(x => x.Used).HasColumnName("used").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("idx_reset_token_hash");
            e.HasIndex(x => x.UserId).HasDatabaseName("idx_reset_user");

            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Application>(e =>
        {
            e.ToTable("applications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.ClientId).HasColumnName("client_id").IsRequired();
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.Type).HasColumnName("type").HasConversion<string>().IsRequired();
            e.Property(x => x.GrantTypes).HasColumnName("grant_types").HasColumnType("text[]").IsRequired();
            e.Property(x => x.RedirectUris).HasColumnName("redirect_uris").HasColumnType("text[]")
                .HasDefaultValueSql("'{}'");
            e.Property(x => x.AllowedScopes).HasColumnName("allowed_scopes").HasColumnType("text[]")
                .HasDefaultValueSql("'{}'");
            e.Property(x => x.TokenLifetime).HasColumnName("token_lifetime").HasDefaultValue(3600);
            e.Property(x => x.IsFirstParty).HasColumnName("is_first_party").HasDefaultValue(false);
            e.Property(x => x.Settings).HasColumnName("settings").HasColumnType("jsonb").HasDefaultValueSql("'{}'");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            e.Ignore(x => x.IsConfidential);

            e.HasIndex(x => x.ClientId).IsUnique().HasDatabaseName("idx_apps_client_id");
            e.HasIndex(x => x.TenantId).HasDatabaseName("idx_apps_tenant");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ApplicationSecret>(e =>
        {
            e.ToTable("application_secrets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.ApplicationId).HasColumnName("application_id").IsRequired();
            e.Property(x => x.SecretHash).HasColumnName("secret_hash").IsRequired();
            e.Property(x => x.Label).HasColumnName("label");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.Revoked).HasColumnName("revoked").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => x.ApplicationId).HasDatabaseName("idx_app_secrets_app");

            e.HasOne(x => x.Application).WithMany(a => a.Secrets).HasForeignKey(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
