using Authly.Core.Common;

namespace Authly.Web.Infrastructure.Api;

/// <summary>Standard paginated list envelope: <c>{ data, total, page, limit }</c> (§6).</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Data, long Total, int Page, int Limit)
{
    public static PagedResponse<T> From<TSource>(PagedResult<TSource> result, Pagination page, Func<TSource, T> map)
        => new(result.Items.Select(map).ToList(), result.Total, page.Page, page.Limit);
}

/// <summary>Standard error envelope: <c>{ error: { code, message } }</c> (§6).</summary>
public sealed record ApiErrorEnvelope(ApiErrorBody Error)
{
    public static ApiErrorEnvelope Of(string code, string message) => new(new ApiErrorBody(code, message));
}

public sealed record ApiErrorBody(string Code, string Message);
