using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FishingSpotsController : ControllerBase
{
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IOutputCacheStore _cacheStore;

    public FishingSpotsController(IRepository<FishingSpot> spotRepository, IOutputCacheStore cacheStore)
    {
        _spotRepository = spotRepository;
        _cacheStore = cacheStore;
    }

    [HttpGet]
    [OutputCache(PolicyName = "MediumCache", Tags = ["fishingspots"])]
    public async Task<ActionResult<IEnumerable<FishingSpotDto>>> GetFishingSpots()
    {
        var spots = await _spotRepository.GetAllAsync();
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
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<FishingSpotDto>> CreateFishingSpot(CreateFishingSpotDto createSpotDto)
    {
        var spot = new FishingSpot
        {
            Name = createSpotDto.Name,
            Description = createSpotDto.Description,
            Latitude = createSpotDto.Latitude,
            Longitude = createSpotDto.Longitude,
            ImageUrl = createSpotDto.ImageUrl,
            PricePerHour = createSpotDto.PricePerHour,
            UserId = createSpotDto.UserId,
            ManagerId = createSpotDto.ManagerId
        };

        await _spotRepository.AddAsync(spot);
        await _cacheStore.EvictByTagAsync("fishingspots", default);
        return CreatedAtAction(nameof(GetFishingSpot), new { id = spot.Id }, MapToDto(spot));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UpdateFishingSpot(int id, UpdateFishingSpotDto updateSpotDto)
    {
        var spot = await _spotRepository.GetByIdAsync(id);
        if (spot == null) return NotFound();

        spot.Name = updateSpotDto.Name ?? spot.Name;
        spot.Description = updateSpotDto.Description ?? spot.Description;
        spot.Latitude = updateSpotDto.Latitude ?? spot.Latitude;
        spot.Longitude = updateSpotDto.Longitude ?? spot.Longitude;
        spot.ImageUrl = updateSpotDto.ImageUrl ?? spot.ImageUrl;
        spot.PricePerHour = updateSpotDto.PricePerHour ?? spot.PricePerHour;
        if (updateSpotDto.ManagerId.HasValue) spot.ManagerId = updateSpotDto.ManagerId;
        else if (updateSpotDto.ManagerId == null && updateSpotDto.Name != null) spot.ManagerId = null;
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
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteFishingSpot(int id)
    {
        if (!await _spotRepository.ExistsAsync(id)) return NotFound();
        
        await _spotRepository.DeleteAsync(id);
        await _cacheStore.EvictByTagAsync("fishingspots", default);
        return NoContent();
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
            ? $"{spot.Manager.FirstName} {spot.Manager.LastName}".Trim().Length > 0
                ? $"{spot.Manager.FirstName} {spot.Manager.LastName}".Trim()
                : spot.Manager.Username
            : null,
        DefaultZoom = spot.DefaultZoom,
        DefaultCenterLat = spot.DefaultCenterLat,
        DefaultCenterLng = spot.DefaultCenterLng,
        FishSpecies = spot.FishSpecies,
        CreatedAt = spot.CreatedAt
    };
}
