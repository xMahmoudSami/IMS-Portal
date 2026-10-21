using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Inventory_Management_System.Models;
using Inventory_Management_System.Models.Webhooks.Events;
using Inventory_Management_System.Services.Recovery;
using Inventory_Management_System.Services.Webhooks;
using Inventory_Management_System.ViewModels;

namespace Inventory_Management_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMasterRecoveryKeyService _recoveryService;
        private readonly IWebhookDispatcher _webhookDispatcher;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IMasterRecoveryKeyService recoveryService,
            IWebhookDispatcher webhookDispatcher,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _recoveryService = recoveryService;
            _webhookDispatcher = webhookDispatcher;
            _logger = logger;
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAppropriateHome();
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Find user by Username or Email
            var user = await _userManager.FindByNameAsync(model.Username)
                       ?? await _userManager.FindByEmailAsync(model.Username);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login credentials.");
                return View(model);
            }

            // Check if user is active
            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact an administrator.");
                return View(model);
            }

            // Attempt login
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                _logger.LogInformation("User {Username} logged in successfully.", user.UserName);

                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return LocalRedirect(model.ReturnUrl);
                }

                return await RedirectUserBasedOnRoleAsync(user);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User {Username} account locked out.", model.Username);
                ModelState.AddModelError(string.Empty, "This account is temporarily locked out due to multiple failed login attempts. Please try again in 15 minutes or contact an administrator.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Invalid login credentials.");
            return View(model);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = 403;
            return View();
        }

        // GET: /Account/ForgotPassword
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordStep1ViewModel());
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordStep1ViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByNameAsync(model.UsernameOrEmail)
                       ?? await _userManager.FindByEmailAsync(model.UsernameOrEmail);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "No account was found matching the provided username or email.");
                return View(model);
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "This account is inactive. Please contact the system administrator.");
                return View(model);
            }

            // Generate temporary 6-digit OTP code for recovery
            var otp = await _recoveryService.GenerateAndStoreOtpAsync(user, validityMinutes: 15);

            // Broadcast Password Reset / OTP event to Webhook receiver (e.g. n8n -> Telegram)
            var eventPayload = new PasswordResetOtpEvent
            {
                UserId = user.Id,
                Username = user.UserName ?? string.Empty,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                OtpCode = otp,
                ExpiresAt = user.RecoveryOtpExpiresAt ?? DateTime.UtcNow.AddMinutes(15),
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                RequestedAt = DateTime.UtcNow,
                DeliveryTarget = "Telegram"
            };

            await _webhookDispatcher.DispatchAsync("auth.password_reset_requested", eventPayload);

            // Render Step 2: Master Recovery Key or OTP Code
            var step2Model = new ForgotPasswordStep2ViewModel
            {
                UserId = user.Id,
                Username = user.UserName ?? string.Empty,
                MaskedEmail = MaskEmail(user.Email),
                OtpDispatchedViaWebhook = true
            };

            return View("ForgotPasswordStep2", step2Model);
        }

        // POST: /Account/ResetPasswordWithRecoveryKey
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPasswordWithRecoveryKey(ForgotPasswordStep2ViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("ForgotPasswordStep2", model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User session expired. Please begin the recovery process again.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            // 1. Verify credentials: Check Master Recovery Key OR 6-digit Telegram OTP
            bool isKeyValid = _recoveryService.VerifyRecoveryKey(user, model.RecoveryKeyOrOtp);
            bool isOtpValid = !isKeyValid && _recoveryService.VerifyOtp(user, model.RecoveryKeyOrOtp);

            if (!isKeyValid && !isOtpValid)
            {
                await _userManager.AccessFailedAsync(user);
                ModelState.AddModelError(nameof(model.RecoveryKeyOrOtp), "The Master Recovery Key or 6-digit OTP code is invalid or has expired.");
                return View("ForgotPasswordStep2", model);
            }

            // 2. Reset password via Identity token
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);

            if (result.Succeeded)
            {
                await _userManager.ResetAccessFailedCountAsync(user);

                // Invalidate the single-use OTP
                await _recoveryService.InvalidateOtpAsync(user);

                // Single-use policy: regenerate a fresh Master Recovery Key for future emergencies
                var newRecoveryKey = await _recoveryService.RegenerateAndStoreKeyAsync(user);

                _logger.LogInformation("Password successfully reset via {Method} for user {Username}. New Master Recovery Key generated.",
                    isKeyValid ? "Master Recovery Key" : "Telegram OTP", user.UserName);

                TempData["NewMasterRecoveryKey"] = newRecoveryKey;
                TempData["SuccessMessage"] = "Your password has been reset successfully! Below is your new Master Recovery Key. Save it immediately!";
                return RedirectToAction(nameof(Login));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("ForgotPasswordStep2", model);
        }

        // GET: /Account/Profile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var roles = await _userManager.GetRolesAsync(user);

            var model = new UserProfileViewModel
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                RoleName = roles.FirstOrDefault() ?? "No Role",
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                HasRecoveryKey = !string.IsNullOrEmpty(user.RecoveryKeyHash),
                RecoveryKeyGeneratedAt = user.RecoveryKeyGeneratedAt,
                NewlyGeneratedRecoveryKey = TempData["NewlyGeneratedRecoveryKey"]?.ToString()
            };

            return View(model);
        }

        // POST: /Account/Profile
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(UserProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            // Always repopulate read-only values for display
            var roles = await _userManager.GetRolesAsync(user);
            model.Username = user.UserName ?? string.Empty;
            model.RoleName = roles.FirstOrDefault() ?? "No Role";
            model.CreatedAt = user.CreatedAt;
            model.LastLoginAt = user.LastLoginAt;
            model.HasRecoveryKey = !string.IsNullOrEmpty(user.RecoveryKeyHash);
            model.RecoveryKeyGeneratedAt = user.RecoveryKeyGeneratedAt;

            // 1. Update basic info
            if (!string.IsNullOrWhiteSpace(model.FullName))
            {
                user.FullName = model.FullName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                user.Email = model.Email.Trim();
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var err in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                return View(model);
            }

            // 2. Update password if requested
            if (!string.IsNullOrEmpty(model.CurrentPassword) && !string.IsNullOrEmpty(model.NewPassword))
            {
                var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                if (!changePasswordResult.Succeeded)
                {
                    foreach (var err in changePasswordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, err.Description);
                    }
                    return View(model);
                }
                await _signInManager.RefreshSignInAsync(user);
                TempData["SuccessMessage"] = "Password updated successfully.";
                return RedirectToAction(nameof(Profile));
            }

            TempData["SuccessMessage"] = "Profile settings updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        // POST: /Account/RegenerateRecoveryKey
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateRecoveryKey()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction(nameof(Login));
            }

            var newKey = await _recoveryService.RegenerateAndStoreKeyAsync(user);
            TempData["NewlyGeneratedRecoveryKey"] = newKey;
            TempData["SuccessMessage"] = "A new Master Recovery Key has been generated. Please store it safely immediately!";

            return RedirectToAction(nameof(Profile));
        }

        private static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return "your registered account";
            var parts = email.Split('@');
            var name = parts[0];
            var domain = parts[1];
            var visible = name.Length > 2 ? name[..2] : name[..1];
            return $"{visible}***@{domain}";
        }

        private IActionResult RedirectToAppropriateHome()
        {
            if (User.IsInRole(AppRoles.Cashier))
            {
                return RedirectToAction("Create", "Sales");
            }
            return RedirectToAction("Analytics", "Dashboard");
        }

        private async Task<IActionResult> RedirectUserBasedOnRoleAsync(ApplicationUser user)
        {
            if (await _userManager.IsInRoleAsync(user, AppRoles.Cashier))
            {
                return RedirectToAction("Create", "Sales");
            }
            return RedirectToAction("Analytics", "Dashboard");
        }
    }
}
