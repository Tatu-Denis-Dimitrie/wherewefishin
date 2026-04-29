using WhereWeFishin.Core.DTOs;

namespace WhereWeFishin.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task<RegistrationConflictType> GetRegistrationConflictAsync(string username, string email);
    Task<bool> UserExistsAsync(string username, string email);
    string GenerateJwtToken(int userId, string username, string email, string role);
    Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request);
}

public enum RegistrationConflictType
{
    None,
    Username,
    Email,
    UsernameAndEmail
}
