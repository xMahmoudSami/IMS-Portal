using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Inventory_Management_System.Models.Webhooks;
using Microsoft.Extensions.Options;

namespace Inventory_Management_System.Services.Webhooks
{
    public class WebhookDispatcher : IWebhookDispatcher
    {
        public const string HttpClientName = "WebhookClient";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebhookSigner _signer;
        private readonly IOptionsMonitor<WebhookOptions> _optionsMonitor;
        private readonly ILogger<WebhookDispatcher> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public WebhookDispatcher(
            IHttpClientFactory httpClientFactory,
            IWebhookSigner signer,
            IOptionsMonitor<WebhookOptions> optionsMonitor,
            ILogger<WebhookDispatcher> logger)
        {
            _httpClientFactory = httpClientFactory;
            _signer = signer;
            _optionsMonitor = optionsMonitor;
            _logger = logger;
        }

        public async Task<bool> DispatchAsync<T>(string eventType, T data, CancellationToken cancellationToken = default)
        {
            var options = _optionsMonitor.CurrentValue;

            // 1. Graceful Fault Tolerance: Check if webhooks are enabled and target URL configured
            if (!options.Enabled)
            {
                _logger.LogInformation("Webhook dispatcher is disabled. Skipping event dispatch for '{EventType}'.", eventType);
                return false;
            }

            if (string.IsNullOrWhiteSpace(options.PayloadUrl))
            {
                _logger.LogWarning("Webhook dispatcher is enabled, but no target URL is configured. Skipping event '{EventType}'.", eventType);
                return false;
            }

            // 2. Wrap event data into standardized envelope
            var envelope = new WebhookPayload<T>
            {
                EventId = Guid.NewGuid().ToString("N"),
                EventType = eventType,
                Timestamp = DateTime.UtcNow,
                Data = data
            };

            string jsonPayload;
            try
            {
                jsonPayload = JsonSerializer.Serialize(envelope, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serialize webhook envelope for event '{EventType}'.", eventType);
                return false;
            }

            // 3. Compute cryptographic HMAC-SHA256 signature
            var signature = _signer.ComputeSignature(jsonPayload, options.Secret);

            // 4. Dispatch HTTP POST non-blockingly with resilience
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                if (options.TimeoutSeconds > 0)
                {
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, options.PayloadUrl)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                // Attach security and identification headers
                if (!string.IsNullOrEmpty(signature))
                {
                    request.Headers.Add("X-Hub-Signature-256", signature);
                }
                request.Headers.Add("X-IMS-Event-Type", eventType);
                request.Headers.Add("X-IMS-Delivery", envelope.EventId);
                request.Headers.Add("X-IMS-Timestamp", envelope.Timestamp.ToString("o"));
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("IMS-Webhook-Dispatcher", "1.0"));

                _logger.LogInformation("Dispatching webhook event '{EventType}' [Delivery: {DeliveryId}] to {Endpoint}...",
                    eventType, envelope.EventId, options.PayloadUrl);

                var response = await client.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully delivered webhook event '{EventType}' [Delivery: {DeliveryId}]. Status: {StatusCode}",
                        eventType, envelope.EventId, (int)response.StatusCode);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Webhook delivery for '{EventType}' [Delivery: {DeliveryId}] received non-success HTTP status: {StatusCode}",
                        eventType, envelope.EventId, (int)response.StatusCode);
                    return false;
                }
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
            {
                _logger.LogWarning("Webhook dispatch for '{EventType}' timed out after {Timeout} seconds.", eventType, options.TimeoutSeconds);
                return false;
            }
            catch (Exception ex)
            {
                // Graceful fault tolerance: never throw to caller / disrupt primary transaction
                _logger.LogError(ex, "An unexpected error occurred while dispatching webhook event '{EventType}' to {Endpoint}.",
                    eventType, options.PayloadUrl);
                return false;
            }
        }
    }
}
