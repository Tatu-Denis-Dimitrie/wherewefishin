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

    [HttpGet("home-overview")]
    public async Task<ActionResult<AdminHomeOverviewDto>> GetHomeOverview()
    {
        var activeUsers = await _context.Users.AsNoTracking().CountAsync();
        var deactivatedUsers = await _context.Users.IgnoreQueryFilters().AsNoTracking().CountAsync(user => user.IsDeleted);
        var totalSpots = await _context.FishingSpots.AsNoTracking().CountAsync();
        var spotsWithoutManager = await _context.FishingSpots.AsNoTracking().CountAsync(s => s.ManagerId == null);
        var rejectedApplications = await _context.ManagerApplications.AsNoTracking().CountAsync(a => a.Status == ManagerApplicationStatus.Rejected);
        var failedAnalyses = await _context.VideoAnalyses.AsNoTracking().CountAsync(a => a.Status == AnalysisStatus.Failed);
        var cancelledBookings = await _context.FishingSessions.AsNoTracking().CountAsync(s => s.Status == SessionStatus.Cancelled);

        var pendingApplications = await _context.ManagerApplications
            .AsNoTracking()
            .Include(application => application.ApplicantUser)
            .Where(application => application.Status == ManagerApplicationStatus.Pending)
            .OrderBy(application => application.CreatedAt)
            .Select(application => new ManagerApplicationDto
            {
                Id = application.Id,
                ApplicantUserId = application.ApplicantUserId,
                ApplicantUsername = application.ApplicantUser.Username,
                ApplicantDisplayName = UserExtensions.GetDisplayName(
                    application.ApplicantUser.FirstName,
                    application.ApplicantUser.LastName,
                    application.ApplicantUser.Username),
                LakeName = application.LakeName,
                Description = application.Description,
                Latitude = application.Latitude,
                Longitude = application.Longitude,
                LocationLabel = application.LocationLabel,
                ProposedPricePerHour = application.ProposedPricePerHour,
                FishSpecies = application.FishSpecies,
                ContactPhone = application.ContactPhone,
                Motivation = application.Motivation,
                AdministrationBasis = application.AdministrationBasis,
                Status = application.Status.ToString(),
                RejectionReason = application.RejectionReason,
                ReviewedAt = application.ReviewedAt,
                ReviewedByAdminId = application.ReviewedByAdminId,
                ApprovedFishingSpotId = application.ApprovedFishingSpotId,
                CreatedAt = application.CreatedAt,
                UpdatedAt = application.UpdatedAt
            })
            .ToListAsync();

        return Ok(new AdminHomeOverviewDto
        {
            ActiveUsers = activeUsers,
            DeactivatedUsers = deactivatedUsers,
            TotalSpots = totalSpots,
            SpotsWithoutManager = spotsWithoutManager,
            PendingManagerApplications = pendingApplications.Count,
            RejectedManagerApplications = rejectedApplications,
            FailedVideoAnalyses = failedAnalyses,
            CancelledBookings = cancelledBookings,
            PendingApplications = pendingApplications
        });
    }

    [HttpGet("stats")]
    public async Task<ActionResult> GetStats()
    {
        var userStats = (await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new UserStatsSnapshot
            {
                TotalUsers = group.Count(user => !user.IsDeleted),
                TotalManagers = group.Count(user => !user.IsDeleted && user.Role == UserRole.Manager),
                TotalAdmins = group.Count(user => !user.IsDeleted && user.Role == UserRole.Admin),
                DeactivatedUsers = group.Count(user => user.IsDeleted)
            })
            .FirstOrDefaultAsync()) ?? new UserStatsSnapshot();

        var analysisStats = (await _context.VideoAnalyses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new AnalysisStatsSnapshot
            {
                TotalAnalyses = group.Count(analysis => !analysis.IsDeleted),
                CompletedAnalyses = group.Count(analysis => !analysis.IsDeleted && analysis.Status == AnalysisStatus.Completed),
                FailedAnalyses = group.Count(analysis => !analysis.IsDeleted && analysis.Status == AnalysisStatus.Failed)
            })
            .FirstOrDefaultAsync()) ?? new AnalysisStatsSnapshot();

        var bookingStats = (await _context.FishingSessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new BookingStatsSnapshot
            {
                TotalBookings = group.Count(session => !session.IsDeleted),
                ConfirmedBookings = group.Count(session => !session.IsDeleted && session.Status == SessionStatus.Confirmed),
                CancelledBookings = group.Count(session => !session.IsDeleted && session.Status == SessionStatus.Cancelled)
            })
            .FirstOrDefaultAsync()) ?? new BookingStatsSnapshot();

        var totalSpots = await _context.FishingSpots.AsNoTracking().CountAsync();
        var totalPontoons = await _context.Pontoons.AsNoTracking().CountAsync();
        var totalReviews = await _context.Reviews.AsNoTracking().CountAsync();

        return Ok(new
        {
            totalUsers = userStats.TotalUsers,
            totalManagers = userStats.TotalManagers,
            totalAdmins = userStats.TotalAdmins,
            deactivatedUsers = userStats.DeactivatedUsers,
            totalAnalyses = analysisStats.TotalAnalyses,
            completedAnalyses = analysisStats.CompletedAnalyses,
            failedAnalyses = analysisStats.FailedAnalyses,
            totalBookings = bookingStats.TotalBookings,
            confirmedBookings = bookingStats.ConfirmedBookings,
            cancelledBookings = bookingStats.CancelledBookings,
            totalSpots,
            totalPontoons,
            totalReviews
        });
    }

    private sealed class UserStatsSnapshot
    {
        public int TotalUsers { get; set; }
        public int TotalManagers { get; set; }
        public int TotalAdmins { get; set; }
        public int DeactivatedUsers { get; set; }
    }

    private sealed class AnalysisStatsSnapshot
    {
        public int TotalAnalyses { get; set; }
        public int CompletedAnalyses { get; set; }
        public int FailedAnalyses { get; set; }
    }

    private sealed class BookingStatsSnapshot
    {
        public int TotalBookings { get; set; }
        public int ConfirmedBookings { get; set; }
        public int CancelledBookings { get; set; }
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
