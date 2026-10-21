namespace Inventory_Management_System.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username or Email is required")]
        [Display(Name = "Username / Email")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class ForgotPasswordStep1ViewModel
    {
        [Required(ErrorMessage = "Username or Email is required")]
        [Display(Name = "Username / Email")]
        public string UsernameOrEmail { get; set; } = string.Empty;
    }

    public class ForgotPasswordStep2ViewModel
    {
        public string UserId { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string MaskedEmail { get; set; } = string.Empty;

        public bool OtpDispatchedViaWebhook { get; set; }

        [Required(ErrorMessage = "Master Recovery Key or OTP Code is required")]
        [Display(Name = "Master Recovery Key or 6-Digit OTP")]
        public string RecoveryKeyOrOtp { get; set; } = string.Empty;

        [Required(ErrorMessage = "New Password is required")]
        [StringLength(100, ErrorMessage = "Password must be at least {2} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class UserProfileViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full Name is required")]
        [Display(Name = "Full Name")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Assigned Role")]
        public string RoleName { get; set; } = string.Empty;

        [Display(Name = "Account Created")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Last Login")]
        public DateTime? LastLoginAt { get; set; }

        // Master Recovery Key status & generation
        public bool HasRecoveryKey { get; set; }
        public DateTime? RecoveryKeyGeneratedAt { get; set; }
        public string? NewlyGeneratedRecoveryKey { get; set; }

        // Password change section
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string? CurrentPassword { get; set; }

        [StringLength(100, ErrorMessage = "Password must be at least {2} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "New password and confirmation do not match.")]
        public string? ConfirmNewPassword { get; set; }
    }
}
