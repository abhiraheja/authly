using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for the single-row <see cref="ObservabilityConfig"/> (global / NO RLS). Implemented in Infrastructure.</summary>
public interface IObservabilityConfigRepository
{
    /// <summary>Returns the singleton config row, or null when it has never been saved.</summary>
    Task<ObservabilityConfig?> GetAsync(CancellationToken ct = default);

    /// <summary>Inserts or updates the singleton config row.</summary>
    Task UpsertAsync(ObservabilityConfig config, CancellationToken ct = default);
}
