using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Security.Claims;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using WhereWeFishin.Tests.TestHelpers;

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
            Role = Roles.User,
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
    public async Task Login_WithInvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        _controller.ModelState.AddModelError(nameof(LoginRequest.Password), "Password is required");

        // Act
        var result = await _controller.Login(new LoginRequest());

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        await _authService.DidNotReceive().LoginAsync(Arg.Any<LoginRequest>());
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
            Role = Roles.User,
            UserId = 5,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
        _authService.GetRegistrationConflictAsync(request.Username, request.Email).Returns(RegistrationConflictType.None);
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
    public async Task Register_WithExistingUsername_ReturnsConflictWithSpecificMessage()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "existinguser",
            Email = "existing@test.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };
        _authService.GetRegistrationConflictAsync(request.Username, request.Email).Returns(RegistrationConflictType.Username);

        // Act
        var result = await _controller.Register(request);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("An account with this username already exists.", GetMessage(conflictResult.Value));
        await _authService.DidNotReceive().RegisterAsync(Arg.Any<RegisterRequest>());
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsConflictWithSpecificMessage()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "existing@test.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };
        _authService.GetRegistrationConflictAsync(request.Username, request.Email).Returns(RegistrationConflictType.Email);

        // Act
        var result = await _controller.Register(request);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("An account with this email already exists.", GetMessage(conflictResult.Value));
    }

    [Fact]
    public async Task Register_WhenDuplicateDetectedAfterServiceFailure_ReturnsConflict()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "existing@test.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };
        _authService.GetRegistrationConflictAsync(request.Username, request.Email)
            .Returns(RegistrationConflictType.None, RegistrationConflictType.Email);
        _authService.RegisterAsync(request).Returns((AuthResponse?)null);

        // Act
        var result = await _controller.Register(request);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("An account with this email already exists.", GetMessage(conflictResult.Value));
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
        _authService.GetRegistrationConflictAsync(request.Username, request.Email)
            .Returns(RegistrationConflictType.None, RegistrationConflictType.None);
        _authService.RegisterAsync(request).Returns((AuthResponse?)null);

        // Act
        var result = await _controller.Register(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        _controller.ModelState.AddModelError(nameof(RegisterRequest.Email), "Invalid email");

        // Act
        var result = await _controller.Register(new RegisterRequest());

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        await _authService.DidNotReceive().GetRegistrationConflictAsync(Arg.Any<string>(), Arg.Any<string>());
    }


    [Fact]
    public void VerifyToken_WithAuthenticatedUser_ReturnsOkWithUserInfo()
    {
        // Arrange
        ControllerContextFactory.SetAuthenticatedUser(
            _controller,
            userId: 42,
            username: "verifieduser",
            email: "verified@test.com");

        // Act
        var result = _controller.VerifyToken();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    private static string? GetMessage(object? payload)
    {
        return payload?.GetType().GetProperty("message")?.GetValue(payload)?.ToString();
    }

    [Fact]
    public async Task ForgotPassword_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new ForgotPasswordRequest { Email = "user@test.com" };
        _authService.ForgotPasswordAsync(request).Returns(true);

        // Act
        var result = await _controller.ForgotPassword(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        await _authService.Received(1).ForgotPasswordAsync(request);
    }

    [Fact]
    public async Task ForgotPassword_WithInvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        _controller.ModelState.AddModelError(nameof(ForgotPasswordRequest.Email), "Email is required");

        // Act
        var result = await _controller.ForgotPassword(new ForgotPasswordRequest());

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        await _authService.DidNotReceive().ForgotPasswordAsync(Arg.Any<ForgotPasswordRequest>());
    }

    [Fact]
    public async Task ResetPassword_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "user@test.com",
            Code = "123456",
            NewPassword = "newpassword123"
        };
        _authService.ResetPasswordAsync(request).Returns(true);

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        await _authService.Received(1).ResetPasswordAsync(request);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        _controller.ModelState.AddModelError(nameof(ResetPasswordRequest.Code), "Code is required");

        // Act
        var result = await _controller.ResetPassword(new ResetPasswordRequest());

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        await _authService.DidNotReceive().ResetPasswordAsync(Arg.Any<ResetPasswordRequest>());
    }

    [Fact]
    public async Task ResetPassword_WhenServiceReturnsFalse_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Email = "user@test.com",
            Code = "123456",
            NewPassword = "newpassword123"
        };
        _authService.ResetPasswordAsync(request).Returns(false);

        // Act
        var result = await _controller.ResetPassword(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
