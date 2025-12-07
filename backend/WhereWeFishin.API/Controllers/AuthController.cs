using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var response = await _authService.LoginAsync(request);
        if (response == null)
        {
            _logger.LogWarning("Login failed for: {UsernameOrEmail}", request.UsernameOrEmail);
            return Unauthorized(new { message = "Username sau parolă incorectă" });
        }

        return Ok(response);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await _authService.UserExistsAsync(request.Username, request.Email))
            return Conflict(new { message = "Username-ul sau email-ul există deja" });

        var response = await _authService.RegisterAsync(request);
        return response == null 
            ? StatusCode(500, new { message = "Eroare la înregistrare" })
            : CreatedAtAction(nameof(Register), new { id = response.UserId }, response);
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
}
