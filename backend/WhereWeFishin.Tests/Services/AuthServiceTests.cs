using System.Linq.Expressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Core.Services;

namespace WhereWeFishin.Tests.Services;

public class AuthServiceTests
{
    private readonly IRepository<User> _userRepository;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _userRepository = Substitute.For<IRepository<User>>();
        _emailService = Substitute.For<IEmailService>();
        _logger = Substitute.For<ILogger<AuthService>>();
        _emailService.SendWelcomeEmailAsync(Arg.Any<string>(), Arg.Any<string?>())
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
            Role = "User"
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
        Assert.Equal("User", result.Role);
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
            Role = "User"
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
            Role = "User"
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
        var user = new User
        {
            Id = 1,
            Username = "TestUser",
            Email = "test@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass"),
            Role = "User"
        };
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(new List<User> { user });

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
        Assert.Equal("User", result.Role);
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
    public void GenerateJwtToken_ReturnsNonEmptyToken()
    {
        // Act
        var token = _authService.GenerateJwtToken(1, "testuser", "test@test.com", "User");

        // Assert
        Assert.NotEmpty(token);
        // JWT format: header.payload.signature
        Assert.Equal(3, token.Split('.').Length);
    }
}
