using System.Security.Cryptography;
using System.Text;
using Inventory_Management_System.Models;
using Microsoft.AspNetCore.Identity;

namespace Inventory_Management_System.Services.Recovery
{
    public class MasterRecoveryKeyService : IMasterRecoveryKeyService
    {
        private static readonly char[] KeyChars = "23456789ABCDEFGHJKMNPQRSTUVWXYZ".ToCharArray();
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly ILogger<MasterRecoveryKeyService> _logger;

        public MasterRecoveryKeyService(
            UserManager<ApplicationUser> userManager,
            IPasswordHasher<ApplicationUser> passwordHasher,
            ILogger<MasterRecoveryKeyService> logger)
        {
            _userManager = userManager;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        public string GenerateRecoveryKey()
        {
            // 4 groups of 4 characters: IMS-XXXX-XXXX-XXXX-XXXX (16 chars + prefix = 80+ bits entropy)
            var bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);

            var sb = new StringBuilder("IMS-");
            for (int i = 0; i < 16; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    sb.Append('-');
                }
                sb.Append(KeyChars[bytes[i] % KeyChars.Length]);
            }

            return sb.ToString();
        }

        public string GenerateOtpCode()
        {
            // Cryptographically secure 6-digit numeric OTP code
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");
        }

        public async Task<string> RegenerateAndStoreKeyAsync(ApplicationUser user)
        {
            var plainKey = GenerateRecoveryKey();
            var normalizedKey = NormalizeKey(plainKey);

            // Hash using ASP.NET Core Identity's PBKDF2 password hasher
            user.RecoveryKeyHash = _passwordHasher.HashPassword(user, normalizedKey);
            user.RecoveryKeyGeneratedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to update user {Username} with new recovery key hash: {Errors}", user.UserName, errors);
                throw new InvalidOperationException($"Failed to store recovery key: {errors}");
            }

            _logger.LogInformation("Successfully regenerated and stored Master Recovery Key hash for user {Username}.", user.UserName);
            return plainKey;
        }

        public bool VerifyRecoveryKey(ApplicationUser user, string plainRecoveryKey)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.RecoveryKeyHash) || string.IsNullOrWhiteSpace(plainRecoveryKey))
            {
                return false;
            }

            var normalizedKey = NormalizeKey(plainRecoveryKey);
            var result = _passwordHasher.VerifyHashedPassword(user, user.RecoveryKeyHash, normalizedKey);

            return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
        }

        public async Task<string> GenerateAndStoreOtpAsync(ApplicationUser user, int validityMinutes = 15)
        {
            var otp = GenerateOtpCode();
            user.RecoveryOtpCode = otp;
            user.RecoveryOtpExpiresAt = DateTime.UtcNow.AddMinutes(validityMinutes);

            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Generated 6-digit OTP for user {Username}, valid until {ExpiresAt} UTC.", user.UserName, user.RecoveryOtpExpiresAt);

            return otp;
        }

        public bool VerifyOtp(ApplicationUser user, string otpCode)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.RecoveryOtpCode) || string.IsNullOrWhiteSpace(otpCode))
            {
                return false;
            }

            if (!user.RecoveryOtpExpiresAt.HasValue || user.RecoveryOtpExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Recovery OTP for user {Username} has expired.", user.UserName);
                return false;
            }

            return string.Equals(user.RecoveryOtpCode.Trim(), otpCode.Trim(), StringComparison.Ordinal);
        }

        public async Task InvalidateOtpAsync(ApplicationUser user)
        {
            user.RecoveryOtpCode = null;
            user.RecoveryOtpExpiresAt = null;
            await _userManager.UpdateAsync(user);
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            // Strip any prefix, dashes, spaces, and normalize to uppercase
            return key.Replace("IMS-", "", StringComparison.OrdinalIgnoreCase)
                      .Replace("-", "")
                      .Replace(" ", "")
                      .Trim()
                      .ToUpperInvariant();
        }
    }
}
