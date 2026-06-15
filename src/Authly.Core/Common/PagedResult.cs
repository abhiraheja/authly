namespace Authly.Core.Common;

/// <summary>A page of results plus the total row count, for paginated list endpoints.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, long Total)
{
    public static PagedResult<T> Empty { get; } = new(Array.Empty<T>(), 0);
}

/// <summary>Normalized pagination request (1-based page, bounded limit).</summary>
public readonly record struct Pagination(int Page, int Limit)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    /// <summary>Clamps caller-supplied values to safe bounds (page ≥ 1, 1 ≤ limit ≤ 200).</summary>
    public static Pagination Of(int? page, int? limit)
        => new(Math.Max(1, page ?? 1), Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit));

    public int Skip => (Page - 1) * Limit;
}
