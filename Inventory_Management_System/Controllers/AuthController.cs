using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Inventory_Management_System.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ITokenService tokenService,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _logger = logger;
        }

        // POST: /api/auth/token
        [HttpPost("token")]
        [AllowAnonymous]
        public async Task<IActionResult> GetToken([FromBody] TokenRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Username and password are required." });
            }

            var user = await _userManager.FindByNameAsync(request.Username)
                       ?? await _userManager.FindByEmailAsync(request.Username);

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }

            if (!user.IsActive)
            {
                return Unauthorized(new { message = "Account is inactive. Please contact your system administrator." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                {
                    return Unauthorized(new { message = "Account is temporarily locked out due to multiple failed attempts." });
                }
                return Unauthorized(new { message = "Invalid username or password." });
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var (token, expiration) = await _tokenService.GenerateTokenAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            _logger.LogInformation("Issued JWT token for user {Username}", user.UserName);

            return Ok(new
            {
                token,
                expiration,
                username = user.UserName,
                fullName = user.FullName,
                email = user.Email,
                roles
            });
        }

        // GET: /api/auth/me
        [HttpGet("me")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetCurrentUser()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            var roles = await _userManager.GetRolesAsync(user);

            return Ok(new
            {
                id = user.Id,
                username = user.UserName,
                fullName = user.FullName,
                email = user.Email,
                roles,
                isActive = user.IsActive,
                createdAt = user.CreatedAt,
                lastLoginAt = user.LastLoginAt
            });
        }
    }

    public class TokenRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
