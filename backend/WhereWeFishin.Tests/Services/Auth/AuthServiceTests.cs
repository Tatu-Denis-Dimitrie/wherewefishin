using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Core.Services;
using WhereWeFishin.Tests.TestHelpers;

namespace WhereWeFishin.Tests.Services;

public class AuthServiceTests
{
    private readonly IRepository<User> _userRepository;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthService _authService;
    private readonly List<User> _users;

    public AuthServiceTests()
    {
        _userRepository = Substitute.For<IRepository<User>>();
        _users = _userRepository.UseInMemoryStore<User>();
        _emailService = Substitute.For<IEmailService>();
        _logger = Substitute.For<ILogger<AuthService>>();
        _emailService.SendWelcomeEmailAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.CompletedTask);
        _emailService.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        var configData = new Dictionary<string, string?>
        {
            { "Jwt:Key", "super-secret-key-that-is-long-enough-for-hmac-sha256" },
            { "Jwt:Issuer", "WhereWeFishin" },
            { "Jwt:Audience", "WhereWeFishinUsers" },
            { "Jwt:ExpirationHours", "24" }
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _authService = new AuthService(_userRepository, _configuration, _emailService, _logger);
    }

    private static User CreateUser(
        int id = 1,
        string username = "testuser",
        string email = "test@test.com",
        string password = "password123",
        UserRole role = UserRole.User,
        string? firstName = null,
        string? lastName = null) => new()
    {
        Id = id,
        Username = username,
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        Role = role,
        FirstName = firstName,
        LastName = lastName
    };


    [Fact]
    public async Task LoginAsync_WithValidUsername_ReturnsAuthResponse()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correctpassword"),
            Role = UserRole.User
        };
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(new List<User> { user });

        var request = new LoginRequest
        {
            UsernameOrEmail = "testuser",
            Password = "correctpassword"
        };

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("testuser", result.Username);
        Assert.Equal("test@test.com", result.Email);
        Assert.Equal(Roles.User, result.Role);
        Assert.Equal(1, result.UserId);
        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public async Task LoginAsync_WithValidEmail_ReturnsAuthResponse()
    {
        // Arrange
        var user = new User
        {
            Id = 2,
            Username = "anotheruser",
            Email = "another@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("mypassword"),
            Role = UserRole.User
        };
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(new List<User> { user });

        var request = new LoginRequest
        {
            UsernameOrEmail = "another@test.com",
            Password = "mypassword"
        };

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("anotheruser", result.Username);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNull()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correctpassword"),
            Role = UserRole.User
        };
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(new List<User> { user });

        var request = new LoginRequest
        {
            UsernameOrEmail = "testuser",
            Password = "wrongpassword"
        };

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ReturnsNull()
    {
        // Arrange
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(new List<User>());

        var request = new LoginRequest
        {
            UsernameOrEmail = "ghost",
            Password = "anypassword"
        };

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_IsCaseInsensitiveForUsername()
    {
        // Arrange
        _users.Add(CreateUser(username: "TestUser", password: "pass"));

        var request = new LoginRequest
        {
            UsernameOrEmail = "testuser",
            Password = "pass"
        };

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task LoginAsync_IsCaseInsensitiveForEmail()
    {
        // Arrange
        _users.Add(CreateUser(email: "Test@Example.com", password: "pass"));

        var request = new LoginRequest
        {
            UsernameOrEmail = "test@example.com",
            Password = "pass"
        };

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test@Example.com", result.Email);
    }


    [Fact]
    public async Task RegisterAsync_WithNewUser_ReturnsAuthResponse()
    {
        // Arrange
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(new List<User>());
        _userRepository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<User>());

        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "new@test.com",
            Password = "password123",
            ConfirmPassword = "password123",
            FirstName = "New",
            LastName = "User"
        };

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("newuser", result.Username);
        Assert.Equal("new@test.com", result.Email);
        Assert.Equal(Roles.User, result.Role);
        Assert.NotEmpty(result.Token);
        await _emailService.Received(1).SendWelcomeEmailAsync("new@test.com", "New");
    }

    [Fact]
    public async Task RegisterAsync_WhenWelcomeEmailFails_ReturnsAuthResponse()
    {
        // Arrange
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(new List<User>());
        _userRepository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<User>());
        _emailService.SendWelcomeEmailAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP unavailable")));

        var request = new RegisterRequest
        {
            Username = "newuser2",
            Email = "new2@test.com",
            Password = "password123",
            ConfirmPassword = "password123",
            FirstName = "New",
            LastName = "User"
        };

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("new2@test.com", result.Email);
        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public async Task RegisterAsync_WithExistingUsername_ReturnsNull()
    {
        // Arrange
        var existingUser = new User
        {
            Username = "existinguser",
            Email = "existing@test.com",
            PasswordHash = "hash"
        };
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(new List<User> { existingUser });

        var request = new RegisterRequest
        {
            Username = "existinguser",
            Email = "different@test.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ReturnsNull()
    {
        // Arrange
        var existingUser = new User
        {
            Username = "someuser",
            Email = "taken@test.com",
            PasswordHash = "hash"
        };
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(new List<User> { existingUser });

        var request = new RegisterRequest
        {
            Username = "brandnewuser",
            Email = "taken@test.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.Null(result);
    }


    [Fact]
    public async Task UserExistsAsync_WhenUserExists_ReturnsTrue()
    {
        // Arrange
        var user = new User { Username = "testuser", Email = "test@test.com", PasswordHash = "hash" };
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(new List<User> { user });

        // Act
        var result = await _authService.UserExistsAsync("testuser", "other@test.com");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task UserExistsAsync_WhenNoMatch_ReturnsFalse()
    {
        // Arrange
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(new List<User>());

        // Act
        var result = await _authService.UserExistsAsync("ghost", "ghost@test.com");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UserExistsAsync_IsCaseInsensitiveForUsernameAndEmail()
    {
        // Arrange
        _users.Add(CreateUser(username: "MixedCaseUser", email: "Mixed@Example.com"));

        // Act
        var result = await _authService.UserExistsAsync("mixedcaseuser", "mixed@example.com");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithExistingUser_SetsResetCodeAndSendsEmail()
    {
        // Arrange
        var user = CreateUser(firstName: "Ana");
        _users.Add(user);

        var request = new ForgotPasswordRequest
        {
            Email = user.Email
        };

        // Act
        var result = await _authService.ForgotPasswordAsync(request);

        // Assert
        Assert.True(result);
        Assert.NotNull(user.PasswordResetCode);
        Assert.Equal(6, user.PasswordResetCode.Length);
        Assert.NotNull(user.PasswordResetCodeExpiry);
        Assert.InRange(user.PasswordResetCodeExpiry.Value, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendPasswordResetEmailAsync(user.Email, user.FirstName, user.PasswordResetCode);
    }

    [Fact]
    public async Task ForgotPasswordAsync_IsCaseInsensitiveForEmail()
    {
        // Arrange
        var user = CreateUser(email: "Mixed@Example.com");
        _users.Add(user);

        // Act
        var result = await _authService.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "mixed@example.com" });

        // Assert
        Assert.True(result);
        Assert.NotNull(user.PasswordResetCode);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WithUnknownEmail_ReturnsTrueWithoutSendingEmail()
    {
        // Act
        var result = await _authService.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "missing@test.com" });

        // Assert
        Assert.True(result);
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _emailService.DidNotReceive().SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenEmailSendingFails_ReturnsTrue()
    {
        // Arrange
        var user = CreateUser();
        _users.Add(user);
        _emailService.SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP unavailable")));

        // Act
        var result = await _authService.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email });

        // Assert
        Assert.True(result);
        Assert.NotNull(user.PasswordResetCode);
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidCode_UpdatesPasswordAndClearsResetState()
    {
        // Arrange
        var user = CreateUser(password: "oldpassword");
        user.PasswordResetCode = "123456";
        user.PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(10);
        _users.Add(user);

        var request = new ResetPasswordRequest
        {
            Email = user.Email,
            Code = "123456",
            NewPassword = "newpassword123"
        };

        // Act
        var result = await _authService.ResetPasswordAsync(request);

        // Assert
        Assert.True(result);
        Assert.True(BCrypt.Net.BCrypt.Verify("newpassword123", user.PasswordHash));
        Assert.Null(user.PasswordResetCode);
        Assert.Null(user.PasswordResetCodeExpiry);
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_IsCaseInsensitiveForEmail()
    {
        // Arrange
        var user = CreateUser(email: "Mixed@Example.com");
        user.PasswordResetCode = "123456";
        user.PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(10);
        _users.Add(user);

        // Act
        var result = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = "mixed@example.com",
            Code = "123456",
            NewPassword = "newpassword123"
        });

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithUnknownEmail_ReturnsFalse()
    {
        // Act
        var result = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = "missing@test.com",
            Code = "123456",
            NewPassword = "newpassword123"
        });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithMissingResetCode_ReturnsFalse()
    {
        // Arrange
        _users.Add(CreateUser());

        // Act
        var result = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = "test@test.com",
            Code = "123456",
            NewPassword = "newpassword123"
        });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithExpiredCode_ReturnsFalse()
    {
        // Arrange
        var user = CreateUser();
        user.PasswordResetCode = "123456";
        user.PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(-1);
        _users.Add(user);

        // Act
        var result = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = user.Email,
            Code = "123456",
            NewPassword = "newpassword123"
        });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithWrongCode_ReturnsFalse()
    {
        // Arrange
        var user = CreateUser();
        user.PasswordResetCode = "123456";
        user.PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(10);
        _users.Add(user);

        // Act
        var result = await _authService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = user.Email,
            Code = "654321",
            NewPassword = "newpassword123"
        });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithMissingUser_ReturnsFalse()
    {
        // Act
        var result = await _authService.ChangePasswordAsync(999, new ChangePasswordRequest
        {
            CurrentPassword = "oldpassword",
            NewPassword = "newpassword123"
        });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_ReturnsFalse()
    {
        // Arrange
        _users.Add(CreateUser(password: "oldpassword"));

        // Act
        var result = await _authService.ChangePasswordAsync(1, new ChangePasswordRequest
        {
            CurrentPassword = "wrongpassword",
            NewPassword = "newpassword123"
        });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithValidCurrentPassword_UpdatesPasswordHash()
    {
        // Arrange
        var user = CreateUser(password: "oldpassword");
        _users.Add(user);

        // Act
        var result = await _authService.ChangePasswordAsync(1, new ChangePasswordRequest
        {
            CurrentPassword = "oldpassword",
            NewPassword = "newpassword123"
        });

        // Assert
        Assert.True(result);
        Assert.True(BCrypt.Net.BCrypt.Verify("newpassword123", user.PasswordHash));
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }


    [Fact]
    public void GenerateJwtToken_ReturnsNonEmptyToken()
    {
        // Act
        var token = _authService.GenerateJwtToken(1, "testuser", "test@test.com", Roles.User);

        // Assert
        Assert.NotEmpty(token);
        // JWT format: header.payload.signature
        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public void GenerateJwtToken_ContainsExpectedClaims()
    {
        // Act
        var token = _authService.GenerateJwtToken(42, "angler", "angler@test.com", Roles.Admin);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        Assert.Contains(jwt.Claims, claim => claim.Type == ClaimTypes.NameIdentifier && claim.Value == "42");
        Assert.Contains(jwt.Claims, claim => claim.Type == ClaimTypes.Name && claim.Value == "angler");
        Assert.Contains(jwt.Claims, claim => claim.Type == ClaimTypes.Email && claim.Value == "angler@test.com");
        Assert.Contains(jwt.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == Roles.Admin);
        Assert.Equal("WhereWeFishin", jwt.Issuer);
        Assert.Contains("WhereWeFishinUsers", jwt.Audiences);
    }

    [Fact]
    public void GenerateJwtToken_WhenExpirationConfigIsInvalid_UsesDefaultExpiration()
    {
        // Arrange
        var fallbackConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super-secret-key-that-is-long-enough-for-hmac-sha256",
                ["Jwt:Issuer"] = "WhereWeFishin",
                ["Jwt:Audience"] = "WhereWeFishinUsers",
                ["Jwt:ExpirationHours"] = "invalid"
            })
            .Build();
        var authService = new AuthService(_userRepository, fallbackConfig, _emailService, _logger);
        var issuedAfter = DateTime.UtcNow;

        // Act
        var token = authService.GenerateJwtToken(1, "testuser", "test@test.com", Roles.User);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        Assert.InRange(jwt.ValidTo, issuedAfter.AddHours(23.5), issuedAfter.AddHours(24.5));
    }
}
