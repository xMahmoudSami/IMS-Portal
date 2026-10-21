using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Inventory_Management_System.Services.Recovery;

namespace Inventory_Management_System.Controllers
{
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMasterRecoveryKeyService _recoveryKeyService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IMasterRecoveryKeyService recoveryKeyService,
            ILogger<UsersController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _recoveryKeyService = recoveryKeyService;
            _logger = logger;
        }

        // GET: /Users
        [HttpGet]
        public async Task<IActionResult> Index(string? search = null, string? role = null)
        {
            var currentUserId = _userManager.GetUserId(User);
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                query = query.Where(u =>
                    (u.UserName != null && u.UserName.ToLower().Contains(searchLower)) ||
                    (u.FullName != null && u.FullName.ToLower().Contains(searchLower)) ||
                    (u.Email != null && u.Email.ToLower().Contains(searchLower)));
            }

            var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
            var userList = new List<UserListItemViewModel>();

            foreach (var user in users)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                var primaryRole = userRoles.FirstOrDefault() ?? "None";

                if (!string.IsNullOrWhiteSpace(role) && !string.Equals(primaryRole, role, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

                userList.Add(new UserListItemViewModel
                {
                    Id = user.Id,
                    Username = user.UserName ?? "—",
                    FullName = user.FullName,
                    Email = user.Email ?? "—",
                    Role = primaryRole,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLoginAt,
                    IsLockedOut = isLocked,
                    IsCurrentLoggedInUser = user.Id == currentUserId
                });
            }

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentRole = role;
            ViewBag.Roles = AppRoles.AllRoles;

            return View(userList);
        }

        // GET: /Users/Create
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Roles = AppRoles.AllRoles;
            return View(new CreateUserViewModel());
        }

        // POST: /Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            ViewBag.Roles = AppRoles.AllRoles;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check unique username
            var existingByName = await _userManager.FindByNameAsync(model.Username);
            if (existingByName != null)
            {
                ModelState.AddModelError("Username", $"The username '{model.Username}' is already taken.");
                return View(model);
            }

            // Check unique email
            var existingByEmail = await _userManager.FindByEmailAsync(model.Email);
            if (existingByEmail != null)
            {
                ModelState.AddModelError("Email", $"The email address '{model.Email}' is already in use.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Username.Trim(),
                Email = model.Email.Trim(),
                FullName = model.FullName.Trim(),
                EmailConfirmed = true,
                IsActive = model.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                foreach (var err in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                return View(model);
            }

            // Generate cryptographically secure Master Recovery Key and store hash
            var recoveryKey = await _recoveryKeyService.RegenerateAndStoreKeyAsync(user);

            // Assign role
            if (!string.IsNullOrWhiteSpace(model.Role) && AppRoles.AllRoles.Contains(model.Role))
            {
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            _logger.LogInformation("SuperAdmin created new user {Username} with role {Role}", user.UserName, model.Role);
            TempData["SuccessMessage"] = $"User account for '{user.FullName}' ({user.UserName}) was created successfully.";
            TempData["NewUserRecoveryKey"] = recoveryKey;
            TempData["NewUserName"] = user.UserName;
            return RedirectToAction(nameof(Index));
        }

        // GET: /Users/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var currentUserId = _userManager.GetUserId(User);

            var model = new EditUserViewModel
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? AppRoles.WarehouseManager,
                IsActive = user.IsActive,
                IsCurrentLoggedInUser = user.Id == currentUserId
            };

            ViewBag.Roles = AppRoles.AllRoles;
            return View(model);
        }

        // POST: /Users/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, EditUserViewModel model)
        {
            ViewBag.Roles = AppRoles.AllRoles;

            if (id != model.Id) return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            bool isSelf = user.Id == currentUserId;

            if (!ModelState.IsValid)
            {
                model.IsCurrentLoggedInUser = isSelf;
                return View(model);
            }

            // Guard: Cannot deactivate yourself
            if (isSelf && !model.IsActive)
            {
                ModelState.AddModelError(string.Empty, "You cannot deactivate your own account.");
                model.IsCurrentLoggedInUser = isSelf;
                return View(model);
            }

            // Guard: Cannot remove your own SuperAdmin role if you are the current user
            var currentRoles = await _userManager.GetRolesAsync(user);
            var oldRole = currentRoles.FirstOrDefault();

            if (isSelf && oldRole == AppRoles.SuperAdmin && model.Role != AppRoles.SuperAdmin)
            {
                ModelState.AddModelError(string.Empty, "You cannot remove the SuperAdmin role from your own active session.");
                model.IsCurrentLoggedInUser = isSelf;
                return View(model);
            }

            user.FullName = model.FullName.Trim();
            user.Email = model.Email.Trim();
            user.IsActive = model.IsActive;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var err in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                model.IsCurrentLoggedInUser = isSelf;
                return View(model);
            }

            // Update role if changed
            if (oldRole != model.Role && AppRoles.AllRoles.Contains(model.Role))
            {
                if (!string.IsNullOrEmpty(oldRole))
                {
                    await _userManager.RemoveFromRoleAsync(user, oldRole);
                }
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            _logger.LogInformation("SuperAdmin updated user {Username}.", user.UserName);
            TempData["SuccessMessage"] = $"User details for '{user.UserName}' were successfully updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Users/ToggleStatus/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (user.Id == currentUserId)
            {
                TempData["ErrorMessage"] = "Action rejected: You cannot deactivate your own account.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = !user.IsActive;

            // If reactivating, clear lockout as well
            if (user.IsActive)
            {
                user.LockoutEnd = null;
                await _userManager.ResetAccessFailedCountAsync(user);
            }

            await _userManager.UpdateAsync(user);

            var statusStr = user.IsActive ? "activated" : "deactivated";
            TempData["SuccessMessage"] = $"User '{user.UserName}' has been {statusStr}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Users/ResetPassword/{id}
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new AdminResetPasswordViewModel
            {
                UserId = user.Id,
                Username = user.UserName ?? string.Empty,
                FullName = user.FullName
            };

            return View(model);
        }

        // POST: /Users/ResetPassword/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, AdminResetPasswordViewModel model)
        {
            if (id != model.UserId) return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                model.Username = user.UserName ?? string.Empty;
                model.FullName = user.FullName;
                return View(model);
            }

            // Direct reset without token requirement for admin
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);

            if (result.Succeeded)
            {
                // Clear any lockout and failed attempts
                user.LockoutEnd = null;
                await _userManager.ResetAccessFailedCountAsync(user);

                _logger.LogInformation("SuperAdmin directly reset password for user {Username}", user.UserName);
                TempData["SuccessMessage"] = $"Password for user '{user.UserName}' was reset successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var err in result.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            model.Username = user.UserName ?? string.Empty;
            model.FullName = user.FullName;
            return View(model);
        }

        // POST: /Users/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (user.Id == currentUserId)
            {
                TempData["ErrorMessage"] = "Action rejected: You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            // Guard: Cannot delete the last SuperAdmin
            var isSuperAdmin = await _userManager.IsInRoleAsync(user, AppRoles.SuperAdmin);
            if (isSuperAdmin)
            {
                var superAdmins = await _userManager.GetUsersInRoleAsync(AppRoles.SuperAdmin);
                if (superAdmins.Count <= 1)
                {
                    TempData["ErrorMessage"] = "Action rejected: Cannot delete the only remaining SuperAdmin in the system.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("SuperAdmin deleted user {Username}", user.UserName);
                TempData["SuccessMessage"] = $"User '{user.UserName}' was successfully deleted.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to delete user: {string.Join(", ", result.Errors.Select(e => e.Description))}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
