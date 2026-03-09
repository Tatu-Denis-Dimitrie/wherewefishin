using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using WhereWeFishin.API.Security;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly ITokenRevocationService _tokenRevocationService;

    public AuthController(
        IAuthService authService,
        ILogger<AuthController> logger,
        ITokenRevocationService tokenRevocationService)
    {
        _authService = authService;
        _logger = logger;
        _tokenRevocationService = tokenRevocationService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var response = await _authService.LoginAsync(request);
        if (response == null)
        {
            ExpireAuthCookie();
            _logger.LogWarning("Login failed for: {UsernameOrEmail}", request.UsernameOrEmail);
            return Unauthorized(new { message = "Invalid username or password" });
        }

        SetAuthCookie(response);

        return Ok(response);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _authService.UserExistsAsync(request.Username, request.Email))
            return Conflict(new { message = "Username or email already exists" });

        var response = await _authService.RegisterAsync(request);
        if (response == null)
        {
            return StatusCode(500, new { message = "Registration failed" });
        }

        SetAuthCookie(response);

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

    [HttpPost("logout")]
    [Authorize]
    public ActionResult Logout()
    {
        try
        {
            var token = AuthCookieManager.ReadTokenFromRequest(Request);
            RevokeTokenIfPossible(token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Logout revocation failed, continuing with cookie cleanup");
        }

        ExpireAuthCookie();
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await _authService.ForgotPasswordAsync(request);

        return Ok(new { message = "If this email address is registered, you will receive a verification code." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
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

    private void SetAuthCookie(AuthResponse response)
    {
        AuthCookieManager.SetAuthCookie(
            Response,
            response.Token,
            response.ExpiresAt,
            Request.IsHttps);
    }

    private void ExpireAuthCookie()
    {
        AuthCookieManager.ExpireAuthCookie(Response);
    }

    private void RevokeTokenIfPossible(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            return;
        }

        var jwtToken = handler.ReadJwtToken(token);
        var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(jti))
        {
            return;
        }

        var expiresAtUtc = jwtToken.ValidTo == DateTime.MinValue
            ? DateTime.UtcNow.AddHours(24)
            : jwtToken.ValidTo;

        _tokenRevocationService.RevokeToken(jti, expiresAtUtc);
    }
}
