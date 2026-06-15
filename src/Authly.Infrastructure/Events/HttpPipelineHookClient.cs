using System.Net.Http.Headers;
using System.Text;
using Authly.Core.Events;
using Microsoft.Extensions.Logging;

namespace Authly.Infrastructure.Events;

/// <summary>
/// <see cref="IPipelineHookClient"/> over <see cref="HttpClient"/>. Invokes a hook synchronously
/// with a hard per-call timeout (§4.12), HMAC-signed like webhooks. Never throws — timeout, network
/// error, or non-2xx come back as a non-success result so the caller applies the hook's on-failure
/// policy (continue/block).
/// </summary>
public sealed class HttpPipelineHookClient : IPipelineHookClient
{
    private const int MaxResponseBytes = 64 * 1024; // cap the merged-claims response we read

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpPipelineHookClient> _logger;

    public HttpPipelineHookClient(IHttpClientFactory httpClientFactory, ILogger<HttpPipelineHookClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PipelineHookResult> InvokeAsync(PipelineHookRequest request, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(Math.Clamp(request.TimeoutMs, 250, 30_000)));

            var timestamp = DateTimeOffset.UtcNow;
            var signature = WebhookSigner.Sign(request.Secret, request.Body, timestamp);

            using var client = _httpClientFactory.CreateClient(nameof(HttpPipelineHookClient));
            using var content = new StringContent(request.Body, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var msg = new HttpRequestMessage(HttpMethod.Post, request.Url) { Content = content };
            msg.Headers.TryAddWithoutValidation(WebhookSigner.SignatureHeader, signature);
            msg.Headers.TryAddWithoutValidation(WebhookSigner.TimestampHeader, timestamp.ToUnixTimeSeconds().ToString());
            msg.Headers.TryAddWithoutValidation("X-Authly-Stage", request.Stage);
            msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            msg.Headers.UserAgent.ParseAdd("Authly-Hooks/1.0");

            using var resp = await client.SendAsync(msg, cts.Token);
            var status = (int)resp.StatusCode;
            if (!resp.IsSuccessStatusCode)
                return PipelineHookResult.Fail(status, $"HTTP {status}");

            var body = await ReadCappedAsync(resp, cts.Token);
            return PipelineHookResult.Ok(status, body);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return PipelineHookResult.Fail(null, "timeout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pipeline hook ({Stage}) invocation failed.", request.Stage);
            return PipelineHookResult.Fail(null, ex.GetType().Name);
        }
    }

    private static async Task<string?> ReadCappedAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[MaxResponseBytes];
        var total = 0;
        int read;
        while (total < MaxResponseBytes && (read = await stream.ReadAsync(buffer.AsMemory(total, MaxResponseBytes - total), ct)) > 0)
            total += read;
        return total == 0 ? null : Encoding.UTF8.GetString(buffer, 0, total);
    }
}
