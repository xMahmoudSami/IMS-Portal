using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Inventory_Management_System.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public TokenService(
            IConfiguration configuration,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _configuration = configuration;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<(string Token, DateTime Expiration)> GenerateTokenAsync(ApplicationUser user)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "IMS_SECRET_KEY_FOR_LOCAL_ENTERPRISE_JWT_TOKEN_SIGNING_2026_SECURITY_SHA256!";
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "InventoryManagementSystem";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "InventoryManagementSystemClient";
            var expireMinutes = int.TryParse(_configuration["Jwt:ExpireMinutes"], out var minutes) ? minutes : 480;

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new("FullName", user.FullName ?? string.Empty)
            };

            // Add user roles
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));

                // Add role claims
                var identityRole = await _roleManager.FindByNameAsync(role);
                if (identityRole != null)
                {
                    var roleClaims = await _roleManager.GetClaimsAsync(identityRole);
                    foreach (var roleClaim in roleClaims)
                    {
                        if (!claims.Any(c => c.Type == roleClaim.Type && c.Value == roleClaim.Value))
                        {
                            claims.Add(roleClaim);
                        }
                    }
                }
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(expireMinutes);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expires,
                Issuer = jwtIssuer,
                Audience = jwtAudience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return (tokenString, expires);
        }
    }
}
