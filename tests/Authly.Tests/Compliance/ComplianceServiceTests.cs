using Authly.Core.Compliance;
using Authly.Modules.Common;
using Authly.Modules.Compliance;

namespace Authly.Tests.Compliance;

public class ComplianceServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    // --- ConsentService -----------------------------------------------------

    [Fact]
    public async Task Signup_consent_records_terms_and_privacy_and_audits_each()
    {
        var repo = new FakeConsentRepo();
        var audit = new RecordingAudit();
        var svc = new ConsentService(repo, audit);

        await svc.RecordSignupConsentAsync(Tenant, UserId, "v1", AuditContext.System);

        Assert.Equal(2, repo.Items.Count);
        Assert.Contains(repo.Items, c => c.Purpose == ConsentPurposes.TermsOfService && c.Granted && c.Version == "v1");
        Assert.Contains(repo.Items, c => c.Purpose == ConsentPurposes.PrivacyPolicy && c.Granted);
        Assert.Equal(new[] { "user.consent_recorded", "user.consent_recorded" }, audit.Events);
        Assert.All(repo.Items, c => Assert.Equal(Tenant, c.TenantId));
    }

    // --- DataRightsService --------------------------------------------------

    [Fact]
    public async Task Export_returns_store_payload_and_audits()
    {
        var store = new FakeComplianceStore
        {
            ExportResult = new UserDataExport(DateTimeOffset.UtcNow,
                new ExportedProfile(UserId, "a@x.com", true, null, null, false, "Active", null, null, "UTC", "en", "{}",
                    DateTimeOffset.UtcNow, null),
                Array.Empty<string>(), Array.Empty<ExportedSession>(), Array.Empty<ExportedLogin>(),
                Array.Empty<ExportedMfaFactor>(), Array.Empty<ExportedSocialIdentity>(),
                Array.Empty<ExportedRecoveryContact>(), Array.Empty<ExportedConsent>())
        };
        var audit = new RecordingAudit();
        var svc = new DataRightsService(store, audit);

        var export = await svc.ExportAsync(Tenant, UserId, AuditContext.System);

        Assert.NotNull(export);
        Assert.Equal("a@x.com", export!.Profile.Email);
        Assert.Contains("user.data_exported", audit.Events);
    }

    [Fact]
    public async Task Export_of_missing_user_returns_null_and_does_not_audit()
    {
        var store = new FakeComplianceStore { ExportResult = null };
        var audit = new RecordingAudit();
        var svc = new DataRightsService(store, audit);

        Assert.Null(await svc.ExportAsync(Tenant, UserId, AuditContext.System));
        Assert.Empty(audit.Events);
    }

    [Fact]
    public async Task Erase_deletes_then_audits_and_reports_not_found()
    {
        var store = new FakeComplianceStore { EraseResult = true };
        var audit = new RecordingAudit();
        var svc = new DataRightsService(store, audit);

        Assert.True(await svc.EraseAsync(Tenant, UserId, AuditContext.System));
        Assert.Equal(1, store.EraseCalls);
        Assert.Contains("user.erased", audit.Events);

        // A miss neither double-counts nor writes a misleading audit entry.
        store.EraseResult = false;
        audit.Events.Clear();
        Assert.False(await svc.EraseAsync(Tenant, UserId, AuditContext.System));
        Assert.DoesNotContain("user.erased", audit.Events);
    }
}
