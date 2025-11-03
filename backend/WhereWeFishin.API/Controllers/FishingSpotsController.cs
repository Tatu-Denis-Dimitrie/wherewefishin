using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FishingSpotsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FishingSpotsController> _logger;

    public FishingSpotsController(IUnitOfWork unitOfWork, ILogger<FishingSpotsController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FishingSpotDto>>> GetFishingSpots()
    {
        var spots = await _unitOfWork.FishingSpots.GetAllAsync();
        var spotDtos = spots.Select(s => new FishingSpotDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            ImageUrl = s.ImageUrl,
            UserId = s.UserId,
            CreatedAt = s.CreatedAt
        });

        return Ok(spotDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FishingSpotDto>> GetFishingSpot(Guid id)
    {
        var spot = await _unitOfWork.FishingSpots.GetByIdAsync(id);
        if (spot == null)
        {
            return NotFound();
        }

        var spotDto = new FishingSpotDto
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

        return Ok(spotDto);
    }

    [HttpPost]
    public async Task<ActionResult<FishingSpotDto>> CreateFishingSpot(CreateFishingSpotDto createSpotDto)
    {
        // TODO: Get userId from authenticated user
        var userId = Guid.NewGuid(); // Temporary, should come from auth

        var spot = new FishingSpot
        {
            Name = createSpotDto.Name,
            Description = createSpotDto.Description,
            Latitude = createSpotDto.Latitude,
            Longitude = createSpotDto.Longitude,
            ImageUrl = createSpotDto.ImageUrl,
            UserId = userId
        };

        await _unitOfWork.FishingSpots.AddAsync(spot);
        await _unitOfWork.SaveChangesAsync();

        var spotDto = new FishingSpotDto
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

        return CreatedAtAction(nameof(GetFishingSpot), new { id = spot.Id }, spotDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateFishingSpot(Guid id, UpdateFishingSpotDto updateSpotDto)
    {
        var spot = await _unitOfWork.FishingSpots.GetByIdAsync(id);
        if (spot == null)
        {
            return NotFound();
        }

        if (updateSpotDto.Name != null) spot.Name = updateSpotDto.Name;
        if (updateSpotDto.Description != null) spot.Description = updateSpotDto.Description;
        if (updateSpotDto.Latitude.HasValue) spot.Latitude = updateSpotDto.Latitude.Value;
        if (updateSpotDto.Longitude.HasValue) spot.Longitude = updateSpotDto.Longitude.Value;
        if (updateSpotDto.ImageUrl != null) spot.ImageUrl = updateSpotDto.ImageUrl;

        await _unitOfWork.FishingSpots.UpdateAsync(spot);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFishingSpot(Guid id)
    {
        var exists = await _unitOfWork.FishingSpots.ExistsAsync(id);
        if (!exists)
        {
            return NotFound();
        }

        await _unitOfWork.FishingSpots.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }
}
