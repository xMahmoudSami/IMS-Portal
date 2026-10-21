using System.Text.Json.Serialization;

namespace Inventory_Management_System.Models.Webhooks
{
    /// <summary>
    /// Standardized event envelope for all outgoing webhook notifications.
    /// </summary>
    public class WebhookPayload<T>
    {
        [JsonPropertyName("eventId")]
        public string EventId { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("eventType")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("data")]
        public T Data { get; set; } = default!;
    }
}
