using System.Security.Cryptography;
using System.Text;

namespace Inventory_Management_System.Services.Webhooks
{
    public class WebhookSigner : IWebhookSigner
    {
        public string ComputeSignature(string payload, string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                return string.Empty;
            }

            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var payloadBytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);

            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(payloadBytes);
            var hexSignature = Convert.ToHexString(hashBytes).ToLowerInvariant();

            return $"sha256={hexSignature}";
        }

        public bool VerifySignature(string payload, string secret, string signature)
        {
            if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
            {
                return false;
            }

            var expected = ComputeSignature(payload, secret);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var actualBytes = Encoding.UTF8.GetBytes(signature);

            return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }
    }
}
