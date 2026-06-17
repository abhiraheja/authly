using Authly.Core.Compliance;
using Authly.Core.Monitoring;

namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>Read-only instance monitoring snapshot for the console.</summary>
public sealed class MonitoringViewModel
{
    public HealthReport Health { get; set; } = new(Array.Empty<DependencyHealth>());

    /// <summary>Counts for the active project (users/apps/sessions).</summary>
    public InstanceMetrics Metrics { get; set; } = new(0, 0, 0, 0);

    /// <summary>Number of projects in the operator's organization.</summary>
    public int ProjectCount { get; set; }

    public IReadOnlyList<DailyLoginStat> Analytics { get; set; } = Array.Empty<DailyLoginStat>();
    public string Version { get; set; } = "—";
}
