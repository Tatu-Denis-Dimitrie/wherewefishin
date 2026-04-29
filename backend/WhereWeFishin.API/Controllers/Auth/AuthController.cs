using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthEndpoints")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var response = await _authService.LoginAsync(request);
        if (response == null)
        {
            _logger.LogWarning("Login failed for: {UsernameOrEmail}", request.UsernameOrEmail);
            return Unauthorized(new { message = "Invalid username or password" });
        }

        return Ok(response);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var conflict = await _authService.GetRegistrationConflictAsync(request.Username, request.Email);
        if (conflict != RegistrationConflictType.None)
            return Conflict(new { message = GetRegistrationConflictMessage(conflict) });

        var response = await _authService.RegisterAsync(request);
        if (response == null)
        {
            conflict = await _authService.GetRegistrationConflictAsync(request.Username, request.Email);
            return conflict != RegistrationConflictType.None
                ? Conflict(new { message = GetRegistrationConflictMessage(conflict) })
                : StatusCode(500, new { message = "Registration failed" });
        }

        return CreatedAtAction(nameof(Register), new { id = response.UserId }, response);
    }

    [HttpGet("verify")]
    [Authorize]
    public ActionResult VerifyToken()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

        return Ok(new
        {
            message = "Token valid",
            userId,
            username,
            email
        });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthEndpoints")]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await _authService.ForgotPasswordAsync(request);

        return Ok(new { message = "If this email address is registered, you will receive a verification code." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthEndpoints")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var success = await _authService.ResetPasswordAsync(request);
        if (!success)
        {
            _logger.LogWarning("Password reset failed for email: {Email}", request.Email);
            return BadRequest(new { message = "Invalid or expired code." });
        }

        return Ok(new { message = "Password has been reset successfully." });
    }

    private static string GetRegistrationConflictMessage(RegistrationConflictType conflict)
    {
        return conflict switch
        {
            RegistrationConflictType.Username => "An account with this username already exists.",
            RegistrationConflictType.Email => "An account with this email already exists.",
            RegistrationConflictType.UsernameAndEmail => "An account with this username and email already exists.",
            _ => "Username or email already exists."
        };
    }
}
