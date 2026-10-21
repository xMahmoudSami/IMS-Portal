using System.Text.Json.Serialization;

namespace Inventory_Management_System.Models.Webhooks.Events
{
    /// <summary>
    /// Event payload dispatched when an account password recovery or OTP is requested,
    /// enabling external automation systems (e.g. n8n -> Telegram) to deliver recovery codes.
    /// </summary>
    public class PasswordResetOtpEvent
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("fullName")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("otpCode")]
        public string OtpCode { get; set; } = string.Empty;

        [JsonPropertyName("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        [JsonPropertyName("requestedAt")]
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("ipAddress")]
        public string? IpAddress { get; set; }

        [JsonPropertyName("deliveryTarget")]
        public string DeliveryTarget { get; set; } = "Telegram";
    }
}
