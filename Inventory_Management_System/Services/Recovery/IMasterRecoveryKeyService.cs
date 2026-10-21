using Inventory_Management_System.Models;

namespace Inventory_Management_System.Services.Recovery
{
    public interface IMasterRecoveryKeyService
    {
        /// <summary>
        /// Generates a cryptographically random Master Recovery Key in the standard format (e.g. IMS-XXXX-XXXX-XXXX-XXXX).
        /// </summary>
        string GenerateRecoveryKey();

        /// <summary>
        /// Generates a 6-digit numeric OTP code for external delivery (e.g. via n8n / Telegram).
        /// </summary>
        string GenerateOtpCode();

        /// <summary>
        /// Generates a new Master Recovery Key for the user, stores the cryptographic hash in the database,
        /// and returns the PLAIN-TEXT key to be presented to the user strictly once.
        /// </summary>
        Task<string> RegenerateAndStoreKeyAsync(ApplicationUser user);

        /// <summary>
        /// Verifies whether the provided plain-text recovery key matches the stored hash for the user.
        /// </summary>
        bool VerifyRecoveryKey(ApplicationUser user, string plainRecoveryKey);

        /// <summary>
        /// Generates and stores a temporary 6-digit recovery OTP for the user (valid for the specified duration).
        /// </summary>
        Task<string> GenerateAndStoreOtpAsync(ApplicationUser user, int validityMinutes = 15);

        /// <summary>
        /// Verifies whether the provided OTP code is valid and not expired.
        /// </summary>
        bool VerifyOtp(ApplicationUser user, string otpCode);

        /// <summary>
        /// Inactivates/consumes the OTP code after successful password reset.
        /// </summary>
        Task InvalidateOtpAsync(ApplicationUser user);
    }
}
