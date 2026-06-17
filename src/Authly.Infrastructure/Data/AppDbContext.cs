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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<LoginHistory> LoginHistory => Set<LoginHistory>();
    public DbSet<VerificationToken> VerificationTokens => Set<VerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ApplicationSecret> ApplicationSecrets => Set<ApplicationSecret>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<MfaFactor> MfaFactors => Set<MfaFactor>();
    public DbSet<MfaBackupCode> MfaBackupCodes => Set<MfaBackupCode>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<MessagingProvider> MessagingProviders => Set<MessagingProvider>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();
    public DbSet<SocialIdentity> SocialIdentities => Set<SocialIdentity>();
    public DbSet<SocialProvider> SocialProviders => Set<SocialProvider>();
    public DbSet<WebhookEndpoint> WebhookEndpoints => Set<WebhookEndpoint>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<PipelineHook> PipelineHooks => Set<PipelineHook>();
    public DbSet<ClaimConfig> ClaimConfigs => Set<ClaimConfig>();
    public DbSet<RecoveryContact> RecoveryContacts => Set<RecoveryContact>();
    public DbSet<PendingContactChange> PendingContactChanges => Set<PendingContactChange>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<PlatformState> PlatformState => Set<PlatformState>();
    public DbSet<AccessPolicy> AccessPolicies => Set<AccessPolicy>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<OperatorRole> OperatorRoles => Set<OperatorRole>();
    public DbSet<OperatorPermission> OperatorPermissions => Set<OperatorPermission>();
    public DbSet<OperatorRolePermission> OperatorRolePermissions => Set<OperatorRolePermission>();
    public DbSet<MemberRole> MemberRoles => Set<MemberRole>();
    public DbSet<AccountInviteToken> AccountInviteTokens => Set<AccountInviteToken>();
    public DbSet<ObservabilityConfig> ObservabilityConfigs => Set<ObservabilityConfig>();

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
            e.Property(x => x.OrganizationId).HasColumnName("organization_id").IsRequired();
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
            // A custom auth domain resolves to exactly one tenant (Postgres treats NULLs as distinct,
            // so tenants without a custom domain are unaffected).
            e.HasIndex(x => x.CustomDomain).IsUnique().HasDatabaseName("idx_tenants_custom_domain");

            e.HasIndex(x => x.OrganizationId).HasDatabaseName("idx_tenants_organization");

            e.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            // Block deleting an org that still owns live projects.
            e.HasOne(x => x.Organization).WithMany(o => o.Tenants).HasForeignKey(x => x.OrganizationId)
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


        // --- Console identity (global / RLS-exempt; read before any tenant is resolved) ---

        b.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Email).HasColumnName("email").IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash");
            e.Property(x => x.FirstName).HasColumnName("first_name");
            e.Property(x => x.LastName).HasColumnName("last_name");
            e.Property(x => x.EmailVerified).HasColumnName("email_verified").HasDefaultValue(false);
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>()
                .HasDefaultValue(AccountStatus.Active).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.Property(x => x.LastLoginAt).HasColumnName("last_login_at");

            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("idx_accounts_email");
        });

        b.Entity<Organization>(e =>
        {
            e.ToTable("organizations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.Slug).HasColumnName("slug").IsRequired();
            e.Property(x => x.OwnerAccountId).HasColumnName("owner_account_id");
            e.Property(x => x.Settings).HasColumnName("settings").HasColumnType("jsonb").HasDefaultValueSql("'{}'");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("idx_organizations_slug");

            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OrganizationMembership>(e =>
        {
            e.ToTable("organization_memberships");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
            e.Property(x => x.OrganizationId).HasColumnName("organization_id").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>()
                .HasDefaultValue(MembershipStatus.Invited).IsRequired();
            e.Property(x => x.InvitedByAccountId).HasColumnName("invited_by_account_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.AccountId, x.OrganizationId }).IsUnique().HasDatabaseName("idx_org_memberships_account_org");
            e.HasIndex(x => x.AccountId).HasDatabaseName("idx_org_memberships_account");

            e.HasOne(x => x.Account).WithMany(a => a.Memberships).HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Organization).WithMany(o => o.Memberships).HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- Operator RBAC (org-scoped; global / NO RLS; separate from end-user RBAC) ---

        b.Entity<OperatorRole>(e =>
        {
            e.ToTable("operator_roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.OrganizationId).HasColumnName("organization_id").IsRequired();
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.IsSystem).HasColumnName("is_system").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique().HasDatabaseName("idx_operator_roles_org_name");
        });

        b.Entity<OperatorPermission>(e =>
        {
            e.ToTable("operator_permissions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.OrganizationId).HasColumnName("organization_id").IsRequired();
            e.Property(x => x.Resource).HasColumnName("resource").IsRequired();
            e.Property(x => x.Action).HasColumnName("action").IsRequired();
            e.Property(x => x.Description).HasColumnName("description");
            e.Ignore(x => x.Name);

            e.HasIndex(x => new { x.OrganizationId, x.Resource, x.Action }).IsUnique().HasDatabaseName("idx_operator_permissions_org_res_act");
        });

        b.Entity<OperatorRolePermission>(e =>
        {
            e.ToTable("operator_role_permissions");
            e.HasKey(x => new { x.OperatorRoleId, x.OperatorPermissionId });
            e.Property(x => x.OperatorRoleId).HasColumnName("operator_role_id");
            e.Property(x => x.OperatorPermissionId).HasColumnName("operator_permission_id");

            e.HasOne(x => x.OperatorRole).WithMany(r => r.RolePermissions).HasForeignKey(x => x.OperatorRoleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.OperatorPermission).WithMany().HasForeignKey(x => x.OperatorPermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MemberRole>(e =>
        {
            e.ToTable("member_roles");
            e.HasKey(x => new { x.OrganizationMembershipId, x.OperatorRoleId });
            e.Property(x => x.OrganizationMembershipId).HasColumnName("organization_membership_id");
            e.Property(x => x.OperatorRoleId).HasColumnName("operator_role_id");
            e.Property(x => x.OrganizationId).HasColumnName("organization_id").IsRequired();
            e.Property(x => x.GrantedByAccountId).HasColumnName("granted_by_account_id");
            e.Property(x => x.GrantedAt).HasColumnName("granted_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => x.OrganizationId).HasDatabaseName("idx_member_roles_org");

            e.HasOne(x => x.OperatorRole).WithMany().HasForeignKey(x => x.OperatorRoleId)
                .OnDelete(DeleteBehavior.Cascade);
            // Removing a membership cascades its role grants.
            e.HasOne<OrganizationMembership>().WithMany().HasForeignKey(x => x.OrganizationMembershipId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AccountInviteToken>(e =>
        {
            e.ToTable("account_invite_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.AccountId).HasColumnName("account_id").IsRequired();
            e.Property(x => x.OrganizationId).HasColumnName("organization_id").IsRequired();
            e.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            e.Property(x => x.Used).HasColumnName("used").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("idx_invite_token_hash");
            e.HasIndex(x => new { x.AccountId, x.OrganizationId }).HasDatabaseName("idx_invite_account_org");

            e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
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
            e.Property(x => x.PostLogoutRedirectUris).HasColumnName("post_logout_redirect_uris").HasColumnType("text[]")
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

        b.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.IsSystem).HasColumnName("is_system").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasDatabaseName("idx_roles_tenant_name");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Permission>(e =>
        {
            e.ToTable("permissions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Resource).HasColumnName("resource").IsRequired();
            e.Property(x => x.Action).HasColumnName("action").IsRequired();
            e.Property(x => x.Description).HasColumnName("description");
            e.Ignore(x => x.Name);

            e.HasIndex(x => new { x.TenantId, x.Resource, x.Action }).IsUnique().HasDatabaseName("idx_permissions_tenant_resource_action");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RolePermission>(e =>
        {
            e.ToTable("role_permissions");
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.Property(x => x.RoleId).HasColumnName("role_id");
            e.Property(x => x.PermissionId).HasColumnName("permission_id");

            e.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.RoleId).HasColumnName("role_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.GrantedBy).HasColumnName("granted_by");
            e.Property(x => x.GrantedAt).HasColumnName("granted_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.UserId }).HasDatabaseName("idx_user_roles_tenant_user");

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ApiKey>(e =>
        {
            e.ToTable("api_keys");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.KeyHash).HasColumnName("key_hash").IsRequired();
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.Scopes).HasColumnName("scopes").HasColumnType("text[]").HasDefaultValueSql("'{}'");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.Revoked).HasColumnName("revoked").HasDefaultValue(false);
            e.Property(x => x.LastUsedAt).HasColumnName("last_used_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => x.KeyHash).IsUnique().HasDatabaseName("idx_api_keys_hash");
            e.HasIndex(x => x.TenantId).HasDatabaseName("idx_api_keys_tenant");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MfaFactor>(e =>
        {
            e.ToTable("mfa_factors");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Type).HasColumnName("type").HasConversion(
                v => FactorTypeToString(v), v => ParseFactorType(v)).IsRequired();
            e.Property(x => x.Secret).HasColumnName("secret");
            e.Property(x => x.CredentialId).HasColumnName("credential_id");
            e.Property(x => x.Status).HasColumnName("status").HasConversion(
                v => v.ToString().ToLowerInvariant(), v => ParseFactorStatus(v))
                .HasDefaultValue(MfaFactorStatus.Pending).IsRequired();
            e.Property(x => x.FriendlyName).HasColumnName("friendly_name");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.Property(x => x.LastUsedAt).HasColumnName("last_used_at");

            e.HasIndex(x => x.UserId).HasDatabaseName("idx_mfa_user");
            e.HasIndex(x => x.TenantId).HasDatabaseName("idx_mfa_tenant");

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RecoveryContact>(e =>
        {
            e.ToTable("recovery_contacts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.Type).HasColumnName("type").HasConversion(
                v => v.ToString().ToLowerInvariant(), v => ParseContactType(v)).IsRequired();
            e.Property(x => x.Value).HasColumnName("value").IsRequired();
            e.Property(x => x.Verified).HasColumnName("verified").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.UserId }).HasDatabaseName("idx_recovery_contacts_user");

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PendingContactChange>(e =>
        {
            e.ToTable("pending_contact_changes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.ChangeType).HasColumnName("change_type").HasConversion(
                v => v.ToString().ToLowerInvariant(), v => ParseContactType(v)).IsRequired();
            e.Property(x => x.NewValue).HasColumnName("new_value").IsRequired();
            e.Property(x => x.VerifyTokenHash).HasColumnName("verify_token_hash").IsRequired();
            e.Property(x => x.CancelTokenHash).HasColumnName("cancel_token_hash").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion(
                v => v.ToString().ToLowerInvariant(), v => ParseContactChangeStatus(v))
                .HasDefaultValue(ContactChangeStatus.Pending).IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.UserId }).HasDatabaseName("idx_pending_contact_changes_user");
            e.HasIndex(x => x.VerifyTokenHash).IsUnique().HasDatabaseName("idx_pending_contact_changes_verify");
            e.HasIndex(x => x.CancelTokenHash).IsUnique().HasDatabaseName("idx_pending_contact_changes_cancel");

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ConsentRecord>(e =>
        {
            e.ToTable("consent_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.Purpose).HasColumnName("purpose").IsRequired();
            e.Property(x => x.Granted).HasColumnName("granted").HasDefaultValue(true);
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.IpAddress).HasColumnName("ip_address");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.UserId }).HasDatabaseName("idx_consent_records_user");

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        // Tenant-scoped ABAC access policies (RLS like users). Conditions stored as jsonb.
        b.Entity<AccessPolicy>(e =>
        {
            e.ToTable("access_policies");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Effect).HasColumnName("effect").HasDefaultValue("allow").IsRequired();
            e.Property(x => x.Action).HasColumnName("action").HasDefaultValue("*").IsRequired();
            e.Property(x => x.ResourceType).HasColumnName("resource_type").HasDefaultValue("*").IsRequired();
            e.Property(x => x.Conditions).HasColumnName("conditions").HasColumnType("jsonb").HasDefaultValueSql("'[]'");
            e.Property(x => x.Priority).HasColumnName("priority").HasDefaultValue(0);
            e.Property(x => x.Enabled).HasColumnName("enabled").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.Enabled }).HasDatabaseName("idx_access_policies_tenant");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        // Tenant-scoped known devices (RLS like users).
        b.Entity<UserDevice>(e =>
        {
            e.ToTable("user_devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.Fingerprint).HasColumnName("fingerprint").IsRequired();
            e.Property(x => x.Label).HasColumnName("label").IsRequired();
            e.Property(x => x.UserAgent).HasColumnName("user_agent");
            e.Property(x => x.LastIp).HasColumnName("last_ip");
            e.Property(x => x.Trusted).HasColumnName("trusted").HasDefaultValue(false);
            e.Property(x => x.FirstSeenAt).HasColumnName("first_seen_at").HasDefaultValueSql("NOW()");
            e.Property(x => x.LastSeenAt).HasColumnName("last_seen_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.UserId, x.Fingerprint }).IsUnique().HasDatabaseName("idx_user_devices_fingerprint");

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        // Instance-global observability config (single row). NOT tenant-scoped — no RLS.
        b.Entity<ObservabilityConfig>(e =>
        {
            e.ToTable("observability_config");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Enabled).HasColumnName("enabled").HasDefaultValue(false);
            e.Property(x => x.Exporter).HasColumnName("exporter").HasDefaultValue("otlp").IsRequired();
            e.Property(x => x.OtlpEndpoint).HasColumnName("otlp_endpoint");
            e.Property(x => x.OtlpHeadersEncrypted).HasColumnName("otlp_headers_encrypted");
            e.Property(x => x.AzureConnectionStringEncrypted).HasColumnName("azure_connection_string_encrypted");
            e.Property(x => x.Signals).HasColumnName("signals").HasDefaultValue("traces,metrics,logs").IsRequired();
            e.Property(x => x.SamplingRatio).HasColumnName("sampling_ratio").HasDefaultValue(1.0);
            e.Property(x => x.LogStreamEndpoint).HasColumnName("log_stream_endpoint");
            e.Property(x => x.LogStreamKeyEncrypted).HasColumnName("log_stream_key_encrypted");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        });

        // Platform-level key/value control-plane state (log-stream cursor, etc). NOT tenant-scoped — no RLS.
        b.Entity<PlatformState>(e =>
        {
            e.ToTable("platform_state");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasColumnName("key");
            e.Property(x => x.Value).HasColumnName("value").IsRequired();
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        });

        b.Entity<MfaBackupCode>(e =>
        {
            e.ToTable("mfa_backup_codes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.CodeHash).HasColumnName("code_hash").IsRequired();
            e.Property(x => x.Used).HasColumnName("used").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => x.UserId).HasDatabaseName("idx_mfa_backup_user");

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OtpCode>(e =>
        {
            e.ToTable("otp_codes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Channel).HasColumnName("channel").HasConversion(
                v => v.ToString().ToLowerInvariant(), v => ParseChannel(v)).IsRequired();
            e.Property(x => x.CodeHash).HasColumnName("code_hash").IsRequired();
            e.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(0);
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
            e.Property(x => x.Used).HasColumnName("used").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.UserId }).HasDatabaseName("idx_otp_tenant_user");

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MessagingProvider>(e =>
        {
            e.ToTable("messaging_providers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Channel).HasColumnName("channel").HasConversion(
                v => v.ToString().ToLowerInvariant(), v => ParseMessageChannel(v)).IsRequired();
            e.Property(x => x.Provider).HasColumnName("provider").IsRequired();
            e.Property(x => x.Mode).HasColumnName("mode").HasDefaultValue("byok").IsRequired();
            e.Property(x => x.Config).HasColumnName("config").HasColumnType("jsonb").HasDefaultValueSql("'{}'");
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.Channel }).HasDatabaseName("idx_messaging_providers_tenant_channel");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MessageTemplate>(e =>
        {
            e.ToTable("message_templates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Key).HasColumnName("key").IsRequired();
            e.Property(x => x.Channel).HasColumnName("channel").HasConversion(
                v => v.ToString().ToLowerInvariant(), v => ParseMessageChannel(v)).IsRequired();
            e.Property(x => x.Locale).HasColumnName("locale").HasDefaultValue("en").IsRequired();
            e.Property(x => x.Subject).HasColumnName("subject");
            e.Property(x => x.Body).HasColumnName("body").IsRequired();
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.Key, x.Channel, x.Locale }).IsUnique()
                .HasDatabaseName("idx_message_templates_unique");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MessageLog>(e =>
        {
            e.ToTable("message_log");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Channel).HasColumnName("channel").HasConversion(
                v => v.ToString().ToLowerInvariant(), v => ParseMessageChannel(v)).IsRequired();
            e.Property(x => x.Recipient).HasColumnName("recipient").IsRequired();
            e.Property(x => x.TemplateKey).HasColumnName("template_key");
            e.Property(x => x.Status).HasColumnName("status").IsRequired();
            e.Property(x => x.Error).HasColumnName("error");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.CreatedAt }).HasDatabaseName("idx_message_log_tenant_created");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SocialIdentity>(e =>
        {
            e.ToTable("social_identities");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Provider).HasColumnName("provider").IsRequired();
            e.Property(x => x.ProviderId).HasColumnName("provider_id").IsRequired();
            e.Property(x => x.ProviderEmail).HasColumnName("provider_email");
            e.Property(x => x.AccessToken).HasColumnName("access_token");
            e.Property(x => x.RefreshToken).HasColumnName("refresh_token");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.RawProfile).HasColumnName("raw_profile").HasColumnType("jsonb").HasDefaultValueSql("'{}'");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            // Per-tenant uniqueness (tenant isolation over the spec's global UNIQUE(provider, provider_id)).
            e.HasIndex(x => new { x.TenantId, x.Provider, x.ProviderId }).IsUnique().HasDatabaseName("idx_social_identity_unique");
            e.HasIndex(x => x.UserId).HasDatabaseName("idx_social_user");

            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SocialProvider>(e =>
        {
            e.ToTable("social_providers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Provider).HasColumnName("provider").IsRequired();
            e.Property(x => x.ClientId).HasColumnName("client_id").IsRequired();
            e.Property(x => x.ClientSecret).HasColumnName("client_secret");
            e.Property(x => x.Scopes).HasColumnName("scopes");
            e.Property(x => x.AuthorizationEndpoint).HasColumnName("authorization_endpoint");
            e.Property(x => x.TokenEndpoint).HasColumnName("token_endpoint");
            e.Property(x => x.UserInfoEndpoint).HasColumnName("user_info_endpoint");
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.Provider }).IsUnique().HasDatabaseName("idx_social_provider_unique");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WebhookEndpoint>(e =>
        {
            e.ToTable("webhook_endpoints");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Url).HasColumnName("url").IsRequired();
            e.Property(x => x.Events).HasColumnName("events").HasColumnType("text[]").HasDefaultValueSql("'{}'");
            e.Property(x => x.Secret).HasColumnName("secret").IsRequired();
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => x.TenantId).HasDatabaseName("idx_webhook_endpoints_tenant");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WebhookDelivery>(e =>
        {
            e.ToTable("webhook_deliveries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.EndpointId).HasColumnName("endpoint_id").IsRequired();
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Event).HasColumnName("event").IsRequired();
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").HasDefaultValueSql("'{}'");
            e.Property(x => x.Status).HasColumnName("status").HasConversion(
                v => v.ToString().ToLowerInvariant(), v => ParseDeliveryStatus(v))
                .HasDefaultValue(WebhookDeliveryStatus.Pending).IsRequired();
            e.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(0);
            e.Property(x => x.ResponseCode).HasColumnName("response_code");
            e.Property(x => x.LastError).HasColumnName("last_error");
            e.Property(x => x.NextRetryAt).HasColumnName("next_retry_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.CreatedAt }).HasDatabaseName("idx_webhook_deliveries_tenant");
            e.HasIndex(x => x.EndpointId).HasDatabaseName("idx_webhook_deliveries_endpoint");

            e.HasOne<WebhookEndpoint>().WithMany().HasForeignKey(x => x.EndpointId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PipelineHook>(e =>
        {
            e.ToTable("pipeline_hooks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.Stage).HasColumnName("stage").HasConversion(
                v => StageToString(v), v => ParseStage(v)).IsRequired();
            e.Property(x => x.Url).HasColumnName("url").IsRequired();
            e.Property(x => x.Secret).HasColumnName("secret").IsRequired();
            e.Property(x => x.TimeoutMs).HasColumnName("timeout_ms").HasDefaultValue(3000);
            e.Property(x => x.OnFailure).HasColumnName("on_failure").HasConversion(
                v => v.ToString().ToLowerInvariant(), v => ParseFailureMode(v))
                .HasDefaultValue(HookFailureMode.Continue).IsRequired();
            e.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.Stage }).HasDatabaseName("idx_pipeline_hooks_tenant_stage");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ClaimConfig>(e =>
        {
            e.ToTable("claim_configs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(x => x.ApplicationId).HasColumnName("application_id");
            e.Property(x => x.TokenType).HasColumnName("token_type").HasConversion(
                v => v.ToString().ToLowerInvariant(), v => ParseClaimTokenType(v)).IsRequired();
            e.Property(x => x.Type).HasColumnName("type").HasConversion(
                v => v.ToString().ToLowerInvariant(), v => ParseClaimSourceType(v)).IsRequired();
            e.Property(x => x.ClaimName).HasColumnName("claim_name").IsRequired();
            e.Property(x => x.Source).HasColumnName("source");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            e.HasIndex(x => new { x.TenantId, x.ApplicationId }).HasDatabaseName("idx_claim_configs_tenant_app");

            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Application>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static WebhookDeliveryStatus ParseDeliveryStatus(string v) => v switch
    {
        "pending" => WebhookDeliveryStatus.Pending,
        "success" => WebhookDeliveryStatus.Success,
        "failed" => WebhookDeliveryStatus.Failed,
        _ => throw new InvalidOperationException($"Unknown webhook delivery status '{v}'.")
    };

    // Pipeline stage maps to fixed snake_case text the schema (§4.12) specifies.
    private static string StageToString(PipelineStage v) => v switch
    {
        PipelineStage.PreRegistration => "pre_registration",
        PipelineStage.PostRegistration => "post_registration",
        PipelineStage.PreLogin => "pre_login",
        PipelineStage.PostLogin => "post_login",
        PipelineStage.PreToken => "pre_token",
        PipelineStage.SendOtp => "send_otp",
        PipelineStage.SendEmail => "send_email",
        _ => throw new InvalidOperationException($"Unknown pipeline stage '{v}'.")
    };

    private static PipelineStage ParseStage(string v) => v switch
    {
        "pre_registration" => PipelineStage.PreRegistration,
        "post_registration" => PipelineStage.PostRegistration,
        "pre_login" => PipelineStage.PreLogin,
        "post_login" => PipelineStage.PostLogin,
        "pre_token" => PipelineStage.PreToken,
        "send_otp" => PipelineStage.SendOtp,
        "send_email" => PipelineStage.SendEmail,
        _ => throw new InvalidOperationException($"Unknown pipeline stage '{v}'.")
    };

    private static HookFailureMode ParseFailureMode(string v) => v switch
    {
        "continue" => HookFailureMode.Continue,
        "block" => HookFailureMode.Block,
        _ => throw new InvalidOperationException($"Unknown hook failure mode '{v}'.")
    };

    private static ClaimTokenType ParseClaimTokenType(string v) => v switch
    {
        "id" => ClaimTokenType.Id,
        "access" => ClaimTokenType.Access,
        _ => throw new InvalidOperationException($"Unknown claim token type '{v}'.")
    };

    private static ClaimSourceType ParseClaimSourceType(string v) => v switch
    {
        "static" => ClaimSourceType.Static,
        "metadata" => ClaimSourceType.Metadata,
        "webhook" => ClaimSourceType.Webhook,
        _ => throw new InvalidOperationException($"Unknown claim source type '{v}'.")
    };

    private static MessageChannel ParseMessageChannel(string v) => v switch
    {
        "email" => MessageChannel.Email,
        "whatsapp" => MessageChannel.WhatsApp,
        _ => throw new InvalidOperationException($"Unknown message channel '{v}'.")
    };

    // MFA factor type maps to fixed snake_case text the schema (§4.4) specifies.
    private static string FactorTypeToString(MfaFactorType v) => v switch
    {
        MfaFactorType.Totp => "totp",
        MfaFactorType.EmailOtp => "email_otp",
        MfaFactorType.WhatsAppOtp => "whatsapp_otp",
        MfaFactorType.Passkey => "passkey",
        _ => throw new InvalidOperationException($"Unknown mfa factor type '{v}'.")
    };

    private static MfaFactorType ParseFactorType(string v) => v switch
    {
        "totp" => MfaFactorType.Totp,
        "email_otp" => MfaFactorType.EmailOtp,
        "whatsapp_otp" => MfaFactorType.WhatsAppOtp,
        "passkey" => MfaFactorType.Passkey,
        _ => throw new InvalidOperationException($"Unknown mfa factor type '{v}'.")
    };

    private static MfaFactorStatus ParseFactorStatus(string v) => v switch
    {
        "pending" => MfaFactorStatus.Pending,
        "active" => MfaFactorStatus.Active,
        "revoked" => MfaFactorStatus.Revoked,
        _ => throw new InvalidOperationException($"Unknown mfa factor status '{v}'.")
    };

    private static OtpChannel ParseChannel(string v) => v switch
    {
        "email" => OtpChannel.Email,
        "whatsapp" => OtpChannel.WhatsApp,
        _ => throw new InvalidOperationException($"Unknown otp channel '{v}'.")
    };

    private static ContactType ParseContactType(string v) => v switch
    {
        "email" => ContactType.Email,
        "phone" => ContactType.Phone,
        _ => throw new InvalidOperationException($"Unknown contact type '{v}'.")
    };

    private static ContactChangeStatus ParseContactChangeStatus(string v) => v switch
    {
        "pending" => ContactChangeStatus.Pending,
        "completed" => ContactChangeStatus.Completed,
        "cancelled" => ContactChangeStatus.Cancelled,
        _ => throw new InvalidOperationException($"Unknown contact change status '{v}'.")
    };
}
