using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IRepository<User> _userRepository;
    private readonly IAuthService _authService;

    public UsersController(IRepository<User> userRepository, IAuthService authService)
    {
        _userRepository = userRepository;
        _authService = authService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
    {
        var users = await _userRepository.GetAllAsync();
        return Ok(users.Select(MapToDto));
    }

    [HttpGet("managers")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetManagers()
    {
        var managers = await _userRepository.FindAsync(u => u.Role == UserRole.Manager);
        return Ok(managers.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user == null ? NotFound() : Ok(MapToDto(user));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, UpdateUserDto updateUserDto)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound();

        user.FirstName = updateUserDto.FirstName ?? user.FirstName;
        user.LastName = updateUserDto.LastName ?? user.LastName;
        user.ProfilePictureUrl = updateUserDto.ProfilePictureUrl ?? user.ProfilePictureUrl;

        await _userRepository.UpdateAsync(user);
        return NoContent();
    }

    [HttpPost("{id}/change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var callerId = User.GetUserId();
        if (callerId != id && !User.IsInRole("Admin"))
            return Forbid();

        var success = await _authService.ChangePasswordAsync(id, request);
        return success ? NoContent() : BadRequest(new { message = "Current password is incorrect." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (!await _userRepository.ExistsAsync(id)) return NotFound();
        
        await _userRepository.DeleteAsync(id);
        return NoContent();
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        ProfilePictureUrl = user.ProfilePictureUrl,
        Role = user.Role.ToString(),
        CreatedAt = user.CreatedAt
    };
}
