using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Extensions;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Database.Context;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class AdminController : ControllerBase
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<VideoAnalysis> _videoRepository;
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IRepository<FishingSession> _sessionRepository;
    private readonly IOutputCacheStore _cacheStore;
    private readonly ApplicationDbContext _context;

    public AdminController(
        IRepository<User> userRepository,
        IRepository<VideoAnalysis> videoRepository,
        IRepository<FishingSpot> spotRepository,
        IRepository<FishingSession> sessionRepository,
        IOutputCacheStore cacheStore,
        ApplicationDbContext context)
    {
        _userRepository = userRepository;
        _videoRepository = videoRepository;
        _spotRepository = spotRepository;
        _sessionRepository = sessionRepository;
        _cacheStore = cacheStore;
        _context = context;
    }

    [HttpGet("stats")]
    public async Task<ActionResult> GetStats()
    {
        // User counts by role (active only)
        var userCounts = await _context.Set<User>()
            .Where(u => !u.IsDeleted)
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalUsers = userCounts.Sum(x => x.Count);
        var totalManagers = userCounts.FirstOrDefault(x => x.Role == UserRole.Manager)?.Count ?? 0;
        var totalAdmins = userCounts.FirstOrDefault(x => x.Role == UserRole.Admin)?.Count ?? 0;
        var deactivatedUsers = await _context.Set<User>().CountAsync(u => u.IsDeleted);

        // Video analysis counts by status
        var analysisCounts = await _context.Set<VideoAnalysis>()
            .Where(v => !v.IsDeleted)
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalAnalyses = analysisCounts.Sum(x => x.Count);
        var completedAnalyses = analysisCounts.FirstOrDefault(x => x.Status == "Completed")?.Count ?? 0;
        var failedAnalyses = analysisCounts.FirstOrDefault(x => x.Status == "Failed")?.Count ?? 0;

        // Session/booking counts by status
        var sessionCounts = await _context.Set<FishingSession>()
            .Where(s => !s.IsDeleted)
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalBookings = sessionCounts.Sum(x => x.Count);
        var confirmedBookings = sessionCounts.FirstOrDefault(x => x.Status == SessionStatus.Confirmed)?.Count ?? 0;
        var cancelledBookings = sessionCounts.FirstOrDefault(x => x.Status == SessionStatus.Cancelled)?.Count ?? 0;

        var totalSpots = await _spotRepository.CountAsync();
        var totalPontoons = await _context.Set<Pontoon>().CountAsync(p => !p.IsDeleted);
        var totalReviews = await _context.Set<Review>().CountAsync(r => !r.IsDeleted);
        var totalCatches = await _context.Set<Catch>().CountAsync(c => !c.IsDeleted);

        return Ok(new
        {
            totalUsers,
            totalManagers,
            totalAdmins,
            deactivatedUsers,
            totalAnalyses,
            completedAnalyses,
            failedAnalyses,
            totalBookings,
            confirmedBookings,
            cancelledBookings,
            totalSpots,
            totalPontoons,
            totalReviews,
            totalCatches
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
            Role = u.Role.ToString(),
            CreatedAt = u.CreatedAt,
            IsActive = !u.IsDeleted
        }));
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> ToggleUserStatus(int id, [FromBody] ToggleStatusDto dto)
    {
        var user = await _userRepository.GetByIdIncludingDeletedAsync(id);
        if (user == null) return NotFound();
        if (user.Role == UserRole.Admin) return BadRequest(new { message = "Cannot disable admin users" });

        user.IsDeleted = !dto.Enable;
        await _userRepository.UpdateAsync(user);

        return Ok(new { message = dto.Enable ? "User enabled" : "User disabled", userId = id });
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> UpdateUserRole(int id, [FromBody] UpdateRoleDto dto)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound();

        if (!Enum.TryParse<UserRole>(dto.Role, ignoreCase: true, out var newRole))
            return BadRequest(new { message = "Invalid role. Valid: User, Employee, Manager, Admin" });

        user.Role = newRole;
        await _userRepository.UpdateAsync(user);

        return Ok(new { message = $"Role updated to {newRole}", userId = id });
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound();
        if (user.Role == UserRole.Admin) return BadRequest(new { message = "Cannot delete admin users" });

        var sessionCount = await _sessionRepository.CountAsync(s => s.UserId == id);
        var videoCount = await _videoRepository.CountAsync(v => v.UserId == id);

        if (sessionCount == 0 && videoCount == 0)
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
                ? UserExtensions.GetDisplayName(s.Manager.FirstName, s.Manager.LastName, s.Manager.Username)
                : null,
            DefaultZoom = s.DefaultZoom,
            DefaultCenterLat = s.DefaultCenterLat,
            DefaultCenterLng = s.DefaultCenterLng,
            FishSpecies = s.FishSpecies,
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
        else if (dto.ClearManager) spot.ManagerId = null;
        if (dto.ResetDefaultMapView)
        {
            spot.DefaultZoom = null;
            spot.DefaultCenterLat = null;
            spot.DefaultCenterLng = null;
        }
        else
        {
            if (dto.DefaultZoom.HasValue) spot.DefaultZoom = dto.DefaultZoom;
            if (dto.DefaultCenterLat.HasValue) spot.DefaultCenterLat = dto.DefaultCenterLat;
            if (dto.DefaultCenterLng.HasValue) spot.DefaultCenterLng = dto.DefaultCenterLng;
        }
        if (dto.FishSpecies != null) spot.FishSpecies = dto.FishSpecies;

        await _spotRepository.UpdateAsync(spot);
        await _cacheStore.EvictByTagAsync("fishingspots", default);

        return Ok(new { message = "Fishing spot updated successfully", spotId = id });
    }

    [HttpDelete("fishing-spots/{id}")]
    public async Task<IActionResult> DeleteFishingSpot(int id)
    {
        var spot = await _spotRepository.GetByIdAsync(id);
        if (spot == null) return NotFound();

        await _spotRepository.DeleteAsync(id);
        await _cacheStore.EvictByTagAsync("fishingspots", default);
        return NoContent();
    }
}
