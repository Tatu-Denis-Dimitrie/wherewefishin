using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Extensions;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FishingSpotsController : ControllerBase
{
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IRepository<FishingSession> _sessionRepository;
    private readonly IRepository<Review> _reviewRepository;
    private readonly IRepository<Pontoon> _pontoonRepository;
    private readonly IRepository<SpotEmployee> _employeeRepository;
    private readonly IRepository<FishStocking> _stockingRepository;
    private readonly IOutputCacheStore _cacheStore;

    public FishingSpotsController(
        IRepository<FishingSpot> spotRepository,
        IRepository<FishingSession> sessionRepository,
        IRepository<Review> reviewRepository,
        IRepository<Pontoon> pontoonRepository,
        IRepository<SpotEmployee> employeeRepository,
        IRepository<FishStocking> stockingRepository,
        IOutputCacheStore cacheStore)
    {
        _spotRepository = spotRepository;
        _sessionRepository = sessionRepository;
        _reviewRepository = reviewRepository;
        _pontoonRepository = pontoonRepository;
        _employeeRepository = employeeRepository;
        _stockingRepository = stockingRepository;
        _cacheStore = cacheStore;
    }

    [HttpGet]
    [OutputCache(PolicyName = "MediumCache", Tags = ["fishingspots"])]
    public async Task<ActionResult<IEnumerable<FishingSpotDto>>> GetFishingSpots()
    {
        var spots = await _spotRepository.GetAllAsync();
        return Ok(spots.Select(MapToDto));
    }

    [HttpGet("managed")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<ActionResult<IEnumerable<FishingSpotDto>>> GetManagedFishingSpots()
    {
        IEnumerable<FishingSpot> spots;

        if (User.IsInRole(Roles.Admin))
        {
            spots = await _spotRepository.GetAllAsync();
        }
        else
        {
            var userId = User.GetUserId();
            if (userId == null) return Unauthorized();

            spots = await _spotRepository.FindAsync(spot => spot.ManagerId == userId.Value || spot.UserId == userId.Value);
        }

        return Ok(spots.Select(MapToDto));
    }

    [HttpGet("{id}")]
    [OutputCache(PolicyName = "MediumCache", Tags = ["fishingspots"])]
    public async Task<ActionResult<FishingSpotDto>> GetFishingSpot(int id)
    {
        var spot = await _spotRepository.GetByIdAsync(id);
        return spot == null ? NotFound() : Ok(MapToDto(spot));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<FishingSpotDto>> CreateFishingSpot(CreateFishingSpotDto createSpotDto)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        if (!User.IsInRole(Roles.Admin)) return Forbid();

        var spot = new FishingSpot
        {
            Name = createSpotDto.Name,
            Description = createSpotDto.Description,
            Latitude = createSpotDto.Latitude,
            Longitude = createSpotDto.Longitude,
            ImageUrl = createSpotDto.ImageUrl,
            PricePerHour = createSpotDto.PricePerHour,
            UserId = userId.Value,
            ManagerId = createSpotDto.ManagerId
        };

        await _spotRepository.AddAsync(spot);
        await _cacheStore.EvictByTagAsync("fishingspots", default);
        return CreatedAtAction(nameof(GetFishingSpot), new { id = spot.Id }, MapToDto(spot));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> UpdateFishingSpot(int id, UpdateFishingSpotDto updateSpotDto)
    {
        var spot = await _spotRepository.GetByIdAsync(id);
        if (spot == null) return NotFound();

        if (!User.CanManageSpot(spot))
            return Forbid();

        spot.Name = updateSpotDto.Name ?? spot.Name;
        spot.Description = updateSpotDto.Description ?? spot.Description;
        spot.Latitude = updateSpotDto.Latitude ?? spot.Latitude;
        spot.Longitude = updateSpotDto.Longitude ?? spot.Longitude;
        spot.ImageUrl = updateSpotDto.ImageUrl ?? spot.ImageUrl;
        spot.PricePerHour = updateSpotDto.PricePerHour ?? spot.PricePerHour;
        if (updateSpotDto.ManagerId.HasValue) spot.ManagerId = updateSpotDto.ManagerId;
        else if (updateSpotDto.ClearManager) spot.ManagerId = null;
        if (updateSpotDto.ResetDefaultMapView)
        {
            spot.DefaultZoom = null;
            spot.DefaultCenterLat = null;
            spot.DefaultCenterLng = null;
        }
        else
        {
            if (updateSpotDto.DefaultZoom.HasValue) spot.DefaultZoom = updateSpotDto.DefaultZoom;
            if (updateSpotDto.DefaultCenterLat.HasValue) spot.DefaultCenterLat = updateSpotDto.DefaultCenterLat;
            if (updateSpotDto.DefaultCenterLng.HasValue) spot.DefaultCenterLng = updateSpotDto.DefaultCenterLng;
        }
        if (updateSpotDto.FishSpecies != null) spot.FishSpecies = updateSpotDto.FishSpecies;

        await _spotRepository.UpdateAsync(spot);
        await _cacheStore.EvictByTagAsync("fishingspots", default);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> DeleteFishingSpot(int id)
    {
        var spot = await _spotRepository.GetByIdAsync(id);
        if (spot == null) return NotFound();

        if (!User.CanManageSpot(spot))
            return Forbid();
        
        await _spotRepository.DeleteAsync(id);
        await _cacheStore.EvictByTagAsync("fishingspots", default);
        return NoContent();
    }

    [HttpGet("{id}/statistics")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<ActionResult<SpotStatisticsDto>> GetSpotStatistics(int id)
    {
        var spot = await _spotRepository.GetByIdAsync(id);
        if (spot == null) return NotFound();

        var userId = User.GetUserId();
        if (!User.CanManageSpot(spot))
            return Forbid();

        var sessions = await _sessionRepository.FindAsync(s => s.FishingSpotId == id);
        var sessionsList = sessions.ToList();
        var reviews = await _reviewRepository.FindAsync(r => r.FishingSpotId == id);
        var reviewsList = reviews.ToList();
        var pontoons = await _pontoonRepository.CountAsync(p => p.FishingSpotId == id);
        var employees = await _employeeRepository.CountAsync(e => e.FishingSpotId == id);
        var stockings = await _stockingRepository.CountAsync(s => s.FishingSpotId == id);

        return Ok(new SpotStatisticsDto
        {
            TotalBookings = sessionsList.Count,
            ActiveBookings = sessionsList.Count(s => s.Status == SessionStatus.Confirmed || s.Status == SessionStatus.Pending),
            CancelledBookings = sessionsList.Count(s => s.Status == SessionStatus.Cancelled),
            TotalRevenue = sessionsList.Where(s => s.Status != SessionStatus.Cancelled).Sum(s => s.TotalPrice),
            TotalReviews = reviewsList.Count,
            AverageRating = reviewsList.Count > 0 ? reviewsList.Average(r => r.Rating) : null,
            TotalPontoons = pontoons,
            TotalEmployees = employees,
            TotalStockings = stockings
        });
    }

    private static FishingSpotDto MapToDto(FishingSpot spot) => new()
    {
        Id = spot.Id,
        Name = spot.Name,
        Description = spot.Description,
        Latitude = spot.Latitude,
        Longitude = spot.Longitude,
        ImageUrl = spot.ImageUrl,
        PricePerHour = spot.PricePerHour,
        UserId = spot.UserId,
        ManagerId = spot.ManagerId,
        ManagerName = spot.Manager != null
            ? UserExtensions.GetDisplayName(spot.Manager.FirstName, spot.Manager.LastName, spot.Manager.Username)
            : null,
        DefaultZoom = spot.DefaultZoom,
        DefaultCenterLat = spot.DefaultCenterLat,
        DefaultCenterLng = spot.DefaultCenterLng,
        FishSpecies = spot.FishSpecies,
        CreatedAt = spot.CreatedAt
    };
}
