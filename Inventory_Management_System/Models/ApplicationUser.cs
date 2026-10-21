using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Inventory_Management_System.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;


        [MaxLength(500)]
        public string? RecoveryKeyHash { get; set; }

        public DateTime? RecoveryKeyGeneratedAt { get; set; }

        [MaxLength(20)]
        public string? RecoveryOtpCode { get; set; }

        public DateTime? RecoveryOtpExpiresAt { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }
    }
}
