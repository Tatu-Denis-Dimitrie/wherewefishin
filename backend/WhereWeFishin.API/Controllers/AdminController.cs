using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<VideoAnalysis> _videoRepository;
    private readonly IRepository<FishingSpot> _spotRepository;

    public AdminController(
        IRepository<User> userRepository,
        IRepository<VideoAnalysis> videoRepository,
        IRepository<FishingSpot> spotRepository)
    {
        _userRepository = userRepository;
        _videoRepository = videoRepository;
        _spotRepository = spotRepository;
    }

    [HttpGet("stats")]
    public async Task<ActionResult> GetStats()
    {
        var users = await _userRepository.GetAllAsync();
        var videos = await _videoRepository.GetAllAsync();
        var spots = await _spotRepository.GetAllAsync();

        var userList = users.ToList();
        var videoList = videos.ToList();

        return Ok(new
        {
            totalUsers = userList.Count,
            totalManagers = userList.Count(u => u.Role == "Manager"),
            totalAdmins = userList.Count(u => u.Role == "Admin"),
            totalAnalyses = videoList.Count,
            completedAnalyses = videoList.Count(v => v.Status == "Completed"),
            failedAnalyses = videoList.Count(v => v.Status == "Failed"),
            totalSpots = spots.Count()
        });
    }

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAllUsers()
    {
        var users = await _userRepository.GetAllAsync();
        return Ok(users.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            ProfilePictureUrl = u.ProfilePictureUrl,
            Role = u.Role,
            CreatedAt = u.CreatedAt
        }));
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleDto dto)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound();

        var validRoles = new[] { "User", "Manager", "Admin" };
        if (!validRoles.Contains(dto.Role))
            return BadRequest(new { message = "Invalid role. Valid: User, Manager, Admin" });

        user.Role = dto.Role;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        return Ok(new { message = $"Role updated to {dto.Role}", userId = id });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound();
        if (user.Role == "Admin") return BadRequest(new { message = "Cannot delete admin users" });

        await _userRepository.DeleteAsync(id);
        return NoContent();
    }
}

public class UpdateRoleDto
{
    public string Role { get; set; } = string.Empty;
}
