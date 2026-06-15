using System.Net.Http.Headers;
using System.Text;
using Authly.Core.Events;
using Microsoft.Extensions.Logging;

namespace Authly.Infrastructure.Events;

/// <summary>
/// <see cref="IWebhookSender"/> over <see cref="HttpClient"/>. POSTs the JSON body with HMAC-SHA256
/// signature + timestamp + delivery-id headers (§4.12). Never throws: network/HTTP failures are
/// returned as a non-success result so the dispatcher can apply the retry ladder. A 20s cap stops a
/// hung endpoint from pinning a worker.
/// </summary>
public sealed class HttpWebhookSender : IWebhookSender
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(20);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpWebhookSender> _logger;

    public HttpWebhookSender(IHttpClientFactory httpClientFactory, ILogger<HttpWebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<WebhookSendResult> SendAsync(WebhookSendRequest request, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(SendTimeout);

            var timestamp = DateTimeOffset.UtcNow;
            var signature = WebhookSigner.Sign(request.Secret, request.Body, timestamp);

            using var client = _httpClientFactory.CreateClient(nameof(HttpWebhookSender));
            using var content = new StringContent(request.Body, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var msg = new HttpRequestMessage(HttpMethod.Post, request.Url) { Content = content };
            msg.Headers.TryAddWithoutValidation(WebhookSigner.SignatureHeader, signature);
            msg.Headers.TryAddWithoutValidation(WebhookSigner.TimestampHeader, timestamp.ToUnixTimeSeconds().ToString());
            msg.Headers.TryAddWithoutValidation(WebhookSigner.DeliveryIdHeader, request.DeliveryId.ToString());
            msg.Headers.TryAddWithoutValidation(WebhookSigner.EventHeader, request.Event);
            msg.Headers.UserAgent.ParseAdd("Authly-Webhooks/1.0");

            using var resp = await client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            var status = (int)resp.StatusCode;
            return resp.IsSuccessStatusCode
                ? WebhookSendResult.Ok(status)
                : WebhookSendResult.Fail(status, $"HTTP {status}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return WebhookSendResult.Fail(null, "timeout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook delivery {DeliveryId} to endpoint failed to send.", request.DeliveryId);
            return WebhookSendResult.Fail(null, ex.GetType().Name);
        }
    }
}
