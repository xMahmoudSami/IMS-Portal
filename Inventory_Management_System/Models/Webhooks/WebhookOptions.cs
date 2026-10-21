namespace Inventory_Management_System.Models.Webhooks
{
    public class WebhookOptions
    {
        public const string SectionName = "WebhookSettings";

        /// <summary>
        /// Global switch to enable or disable webhook dispatching.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Destination endpoint URL (e.g. n8n workflow webhook endpoint).
        /// </summary>
        public string PayloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// Secret key used to compute the HMAC-SHA256 signature attached in the X-Hub-Signature-256 header.
        /// </summary>
        public string Secret { get; set; } = string.Empty;

        /// <summary>
        /// HTTP transmission timeout in seconds (defaults to 10 seconds).
        /// </summary>
        public int TimeoutSeconds { get; set; } = 10;
    }
}
