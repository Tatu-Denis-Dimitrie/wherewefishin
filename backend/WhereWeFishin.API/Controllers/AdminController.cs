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
    private readonly IRepository<FishingSession> _sessionRepository;

    public AdminController(
        IRepository<User> userRepository,
        IRepository<VideoAnalysis> videoRepository,
        IRepository<FishingSpot> spotRepository,
        IRepository<FishingSession> sessionRepository)
    {
        _userRepository = userRepository;
        _videoRepository = videoRepository;
        _spotRepository = spotRepository;
        _sessionRepository = sessionRepository;
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
        var users = await _userRepository.GetAllIncludingDeletedAsync();
        return Ok(users.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            ProfilePictureUrl = u.ProfilePictureUrl,
            Role = u.Role,
            CreatedAt = u.CreatedAt,
            IsActive = !u.IsDeleted
        }));
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> ToggleUserStatus(int id, [FromBody] ToggleStatusDto dto)
    {
        var user = await _userRepository.GetByIdIncludingDeletedAsync(id);
        if (user == null) return NotFound();
        if (user.Role == "Admin") return BadRequest(new { message = "Cannot disable admin users" });

        user.IsDeleted = !dto.Enable;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        return Ok(new { message = dto.Enable ? "User enabled" : "User disabled", userId = id });
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

        var sessions = await _sessionRepository.FindAsync(s => s.UserId == id);
        var videos = await _videoRepository.FindAsync(v => v.UserId == id);

        if (!sessions.Any() && !videos.Any())
            await _userRepository.HardDeleteAsync(id);
        else
            await _userRepository.DeleteAsync(id);

        return NoContent();
    }

    [HttpGet("fishing-spots")]
    public async Task<ActionResult<IEnumerable<FishingSpotDto>>> GetAllFishingSpots()
    {
        var spots = await _spotRepository.GetAllAsync();
        return Ok(spots.Select(s => new FishingSpotDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            ImageUrl = s.ImageUrl,
            PricePerHour = s.PricePerHour,
            UserId = s.UserId,
            ManagerId = s.ManagerId,
            ManagerName = s.Manager != null
                ? $"{s.Manager.FirstName} {s.Manager.LastName}".Trim().Length > 0
                    ? $"{s.Manager.FirstName} {s.Manager.LastName}".Trim()
                    : s.Manager.Username
                : null,
            CreatedAt = s.CreatedAt
        }));
    }

    [HttpPut("fishing-spots/{id}")]
    public async Task<IActionResult> UpdateFishingSpot(int id, [FromBody] UpdateFishingSpotDto dto)
    {
        var spot = await _spotRepository.GetByIdAsync(id);
        if (spot == null) return NotFound();

        if (dto.Name != null) spot.Name = dto.Name;
        if (dto.Description != null) spot.Description = dto.Description;
        if (dto.Latitude.HasValue) spot.Latitude = dto.Latitude.Value;
        if (dto.Longitude.HasValue) spot.Longitude = dto.Longitude.Value;
        if (dto.ImageUrl != null) spot.ImageUrl = dto.ImageUrl;
        if (dto.PricePerHour.HasValue) spot.PricePerHour = dto.PricePerHour.Value;
        if (dto.ManagerId.HasValue) spot.ManagerId = dto.ManagerId.Value;

        spot.UpdatedAt = DateTime.UtcNow;
        await _spotRepository.UpdateAsync(spot);

        return Ok(new { message = "Fishing spot updated successfully", spotId = id });
    }

    [HttpDelete("fishing-spots/{id}")]
    public async Task<IActionResult> DeleteFishingSpot(int id)
    {
        var spot = await _spotRepository.GetByIdAsync(id);
        if (spot == null) return NotFound();

        await _spotRepository.DeleteAsync(id);
        return NoContent();
    }
}

public class UpdateRoleDto
{
    public string Role { get; set; } = string.Empty;
}

public class ToggleStatusDto
{
    public bool Enable { get; set; }
}
