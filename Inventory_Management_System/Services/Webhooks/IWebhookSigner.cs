namespace Inventory_Management_System.Services.Webhooks
{
    public interface IWebhookSigner
    {
        /// <summary>
        /// Computes an HMAC-SHA256 signature for the given payload using the provided secret key.
        /// Returns the formatted signature string (e.g. "sha256=<hex-hash>").
        /// </summary>
        string ComputeSignature(string payload, string secret);

        /// <summary>
        /// Verifies whether the provided signature matches the calculated HMAC-SHA256 signature.
        /// </summary>
        bool VerifySignature(string payload, string secret, string signature);
    }
}
