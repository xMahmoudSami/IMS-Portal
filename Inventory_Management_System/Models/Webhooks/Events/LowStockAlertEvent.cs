using System.Text.Json.Serialization;

namespace Inventory_Management_System.Models.Webhooks.Events
{
    /// <summary>
    /// Event payload dispatched when inventory stock falls to or below threshold.
    /// </summary>
    public class LowStockAlertEvent
    {
        [JsonPropertyName("productId")]
        public int ProductId { get; set; }

        [JsonPropertyName("sku")]
        public string Sku { get; set; } = string.Empty;

        [JsonPropertyName("productName")]
        public string ProductName { get; set; } = string.Empty;

        [JsonPropertyName("currentStock")]
        public int CurrentStock { get; set; }

        [JsonPropertyName("lowStockThreshold")]
        public int LowStockThreshold { get; set; }

        [JsonPropertyName("unitPrice")]
        public decimal UnitPrice { get; set; }

        [JsonPropertyName("categoryName")]
        public string? CategoryName { get; set; }

        [JsonPropertyName("triggerSource")]
        public string TriggerSource { get; set; } = "Transaction";

        [JsonPropertyName("isOutOfStock")]
        public bool IsOutOfStock => CurrentStock <= 0;

        [JsonPropertyName("triggeredAt")]
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    }
}
