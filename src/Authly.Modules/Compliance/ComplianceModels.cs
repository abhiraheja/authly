namespace Authly.Modules.Compliance;

/// <summary>Canonical consent purposes captured at signup.</summary>
public static class ConsentPurposes
{
    public const string TermsOfService = "terms_of_service";
    public const string PrivacyPolicy = "privacy_policy";
    public const string Marketing = "marketing";
}

/// <summary>Aggregate-only telemetry an instance pushes to cloud (§9). Contains NO PII whatsoever.</summary>
public sealed record SyncPayload(
    string Version,
    int TenantCount,
    int UserCount,
    int AppCount,
    int ActiveSessionCount,
    string Status);

/// <summary>Result of registering a self-hosted instance on cloud. The raw key is shown ONCE.</summary>
public sealed record InstanceRegistration(Guid InstanceId, string RawSyncKey);
