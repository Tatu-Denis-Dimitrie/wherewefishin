using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Security.Claims;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace WhereWeFishin.Tests.Controllers;

public class AuthControllerTests
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _authService = Substitute.For<IAuthService>();
        _logger = Substitute.For<ILogger<AuthController>>();
        _controller = new AuthController(_authService, _logger);
    }


    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        // Arrange
        var request = new LoginRequest { UsernameOrEmail = "testuser", Password = "pass123" };
        var authResponse = new AuthResponse
        {
            Token = "jwt-token",
            Username = "testuser",
            Email = "test@test.com",
            Role = "User",
            UserId = 1,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
        _authService.LoginAsync(request).Returns(authResponse);

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedResponse = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.Equal("jwt-token", returnedResponse.Token);
        Assert.Equal("testuser", returnedResponse.Username);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest { UsernameOrEmail = "testuser", Password = "wrongpass" };
        _authService.LoginAsync(request).Returns((AuthResponse?)null);

        // Act
        var result = await _controller.Login(request);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }


    [Fact]
    public async Task Register_WithNewUser_ReturnsCreated()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "new@test.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };
        var authResponse = new AuthResponse
        {
            Token = "new-token",
            Username = "newuser",
            Email = "new@test.com",
            Role = "User",
            UserId = 5,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
        _authService.UserExistsAsync(request.Username, request.Email).Returns(false);
        _authService.RegisterAsync(request).Returns(authResponse);

        // Act
        var result = await _controller.Register(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedResponse = Assert.IsType<AuthResponse>(createdResult.Value);
        Assert.Equal("newuser", returnedResponse.Username);
        Assert.Equal(5, returnedResponse.UserId);
    }

    [Fact]
    public async Task Register_WithExistingUserOrEmail_ReturnsConflict()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "existinguser",
            Email = "existing@test.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };
        _authService.UserExistsAsync(request.Username, request.Email).Returns(true);

        // Act
        var result = await _controller.Register(request);

        // Assert
        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Register_WhenServiceFails_ReturnsInternalServerError()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "new@test.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };
        _authService.UserExistsAsync(request.Username, request.Email).Returns(false);
        _authService.RegisterAsync(request).Returns((AuthResponse?)null);

        // Act
        var result = await _controller.Register(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }


    [Fact]
    public void VerifyToken_WithAuthenticatedUser_ReturnsOkWithUserInfo()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Name, "verifieduser"),
            new Claim(ClaimTypes.Email, "verified@test.com")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        // Act
        var result = _controller.VerifyToken();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
