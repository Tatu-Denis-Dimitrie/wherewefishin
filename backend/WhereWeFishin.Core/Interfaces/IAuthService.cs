using WhereWeFishin.Core.DTOs;

namespace WhereWeFishin.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task<bool> UserExistsAsync(string username, string email);
    string GenerateJwtToken(int userId, string username, string email);
}
