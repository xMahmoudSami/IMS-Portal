namespace Inventory_Management_System.Services
{
    public interface ITokenService
    {
        Task<(string Token, DateTime Expiration)> GenerateTokenAsync(ApplicationUser user);
    }
}
