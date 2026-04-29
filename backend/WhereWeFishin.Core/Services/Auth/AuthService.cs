using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.Core.Services;

public class AuthService : IAuthService
{
    private readonly IRepository<User> _userRepository;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IRepository<User> userRepository,
        IConfiguration configuration,
        IEmailService emailService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _configuration = configuration;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var normalizedUsernameOrEmail = NormalizeLookupValue(request.UsernameOrEmail);
        var users = await _userRepository.FindAsync(u =>
            u.Username.ToLower() == normalizedUsernameOrEmail ||
            u.Email.ToLower() == normalizedUsernameOrEmail);
        var user = users.FirstOrDefault();

        if (user == null)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await GetRegistrationConflictAsync(request.Username, request.Email) != RegistrationConflictType.None)
            return null;

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = UserRole.User,
        };

        try
        {
            await _userRepository.AddAsync(user);
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex, "Registration conflict for username {Username} and email {Email}", request.Username, request.Email);
            return null;
        }

        try
        {
            await _emailService.SendWelcomeEmailAsync(user.Email, user.FirstName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send welcome email to {Email}", user.Email);
        }

        return BuildAuthResponse(user);
    }

    public async Task<RegistrationConflictType> GetRegistrationConflictAsync(string username, string email)
    {
        var normalizedUsername = NormalizeLookupValue(username);
        var normalizedEmail = NormalizeLookupValue(email);
        var users = await _userRepository.FindAsync(u =>
            u.Username.ToLower() == normalizedUsername ||
            u.Email.ToLower() == normalizedEmail);

        var usernameExists = users.Any(user =>
            string.Equals(NormalizeLookupValue(user.Username), normalizedUsername, StringComparison.Ordinal));
        var emailExists = users.Any(user =>
            string.Equals(NormalizeLookupValue(user.Email), normalizedEmail, StringComparison.Ordinal));

        return (usernameExists, emailExists) switch
        {
            (true, true) => RegistrationConflictType.UsernameAndEmail,
            (true, false) => RegistrationConflictType.Username,
            (false, true) => RegistrationConflictType.Email,
            _ => RegistrationConflictType.None
        };
    }

    public async Task<bool> UserExistsAsync(string username, string email)
    {
        return await GetRegistrationConflictAsync(username, email) != RegistrationConflictType.None;
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var normalizedEmail = NormalizeLookupValue(request.Email);
        var users = await _userRepository.FindAsync(u => u.Email.ToLower() == normalizedEmail);
        var user = users.FirstOrDefault();

        if (user == null)
            return true;

        using var rng = RandomNumberGenerator.Create();
        var codeBytes = new byte[4];
        rng.GetBytes(codeBytes);
        var code = (BitConverter.ToUInt32(codeBytes, 0) % 900000 + 100000).ToString();

        user.PasswordResetCode = code;
        user.PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(15);
        await _userRepository.UpdateAsync(user);

        try
        {
            await _emailService.SendPasswordResetEmailAsync(user.Email, user.FirstName, code);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send password reset email to {Email}", user.Email);
        }

        return true;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var normalizedEmail = NormalizeLookupValue(request.Email);
        var users = await _userRepository.FindAsync(u => u.Email.ToLower() == normalizedEmail);
        var user = users.FirstOrDefault();

        if (user == null)
            return false;

        if (string.IsNullOrWhiteSpace(user.PasswordResetCode) ||
            user.PasswordResetCodeExpiry == null ||
            user.PasswordResetCodeExpiry < DateTime.UtcNow)
            return false;

        if (!string.Equals(user.PasswordResetCode, request.Code, StringComparison.Ordinal))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetCode = null;
        user.PasswordResetCodeExpiry = null;
        await _userRepository.UpdateAsync(user);

        return true;
    }

    public string GenerateJwtToken(int userId, string username, string email, string role)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured")));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(GetExpirationHours()),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return false;

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepository.UpdateAsync(user);
        return true;
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var token = GenerateJwtToken(user.Id, user.Username, user.Email, user.Role.ToString());
        var expiresAt = DateTime.UtcNow.AddHours(GetExpirationHours());

        return new AuthResponse
        {
            Token = token,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role.ToString(),
            UserId = user.Id,
            ExpiresAt = expiresAt,
            FirstName = user.FirstName,
            LastName = user.LastName
        };
    }

    private double GetExpirationHours()
    {
        return double.TryParse(_configuration["Jwt:ExpirationHours"], out var hours) ? hours : 24;
    }

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        var message = $"{exception.Message} {exception.InnerException?.Message}";
        return message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("IX_Users_Email", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("IX_Users_Username", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLookupValue(string value) => value.Trim().ToLowerInvariant();
}
