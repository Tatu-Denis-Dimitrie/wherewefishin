using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;
using System.Security.Claims;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FishingSpotsController : ControllerBase
{
    private readonly IRepository<FishingSpot> _spotRepository;

    public FishingSpotsController(IRepository<FishingSpot> spotRepository)
    {
        _spotRepository = spotRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FishingSpotDto>>> GetFishingSpots()
    {
        var spots = await _spotRepository.GetAllAsync();
        return Ok(spots.Select(MapToDto));
    }

    [HttpGet("{id}")]
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
            UserId = createSpotDto.UserId
        };

        await _spotRepository.AddAsync(spot);
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

        await _spotRepository.UpdateAsync(spot);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteFishingSpot(int id)
    {
        if (!await _spotRepository.ExistsAsync(id)) return NotFound();
        
        await _spotRepository.DeleteAsync(id);
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
        UserId = spot.UserId,
        CreatedAt = spot.CreatedAt
    };
}
