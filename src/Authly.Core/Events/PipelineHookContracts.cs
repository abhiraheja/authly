namespace Authly.Core.Events;

/// <summary>
/// Invokes a single pipeline hook synchronously (§4.12). Implemented in Infrastructure (HttpClient)
/// with a hard per-call timeout. Never throws — timeouts/errors/non-2xx come back as a non-success
/// <see cref="PipelineHookResult"/> so the caller can apply the hook's on-failure policy.
/// </summary>
public interface IPipelineHookClient
{
    Task<PipelineHookResult> InvokeAsync(PipelineHookRequest request, CancellationToken ct = default);
}

/// <param name="Url">Hook endpoint.</param>
/// <param name="Secret">Decrypted HMAC secret.</param>
/// <param name="Stage">Stage name (sent as a header).</param>
/// <param name="Body">The exact JSON body to POST and sign.</param>
/// <param name="TimeoutMs">Hard upper bound on the call.</param>
public sealed record PipelineHookRequest(string Url, string Secret, string Stage, string Body, int TimeoutMs);

/// <param name="Success">True on a 2xx response within the timeout.</param>
/// <param name="StatusCode">HTTP status, if a response was received.</param>
/// <param name="ResponseJson">Response body (expected JSON object) on success; used to merge claims.</param>
/// <param name="Error">Short diagnostic on failure.</param>
public sealed record PipelineHookResult(bool Success, int? StatusCode, string? ResponseJson, string? Error)
{
    public static PipelineHookResult Ok(int status, string? json) => new(true, status, json, null);
    public static PipelineHookResult Fail(int? status, string error) => new(false, status, null, error);
}
