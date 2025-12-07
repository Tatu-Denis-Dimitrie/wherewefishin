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

    public FishingSpotsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FishingSpotDto>>> GetFishingSpots()
    {
        var spots = await _unitOfWork.FishingSpots.GetAllAsync();
        return Ok(spots.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FishingSpotDto>> GetFishingSpot(int id)
    {
        var spot = await _unitOfWork.FishingSpots.GetByIdAsync(id);
        return spot == null ? NotFound() : Ok(MapToDto(spot));
    }

    [HttpPost]
    public async Task<ActionResult<FishingSpotDto>> CreateFishingSpot(CreateFishingSpotDto createSpotDto)
    {
        var spot = new FishingSpot
        {
            Name = createSpotDto.Name,
            Description = createSpotDto.Description,
            Latitude = createSpotDto.Latitude,
            Longitude = createSpotDto.Longitude,
            ImageUrl = createSpotDto.ImageUrl,
            UserId = 1 
        };

        await _unitOfWork.FishingSpots.AddAsync(spot);
        await _unitOfWork.SaveChangesAsync();
        return CreatedAtAction(nameof(GetFishingSpot), new { id = spot.Id }, MapToDto(spot));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateFishingSpot(int id, UpdateFishingSpotDto updateSpotDto)
    {
        var spot = await _unitOfWork.FishingSpots.GetByIdAsync(id);
        if (spot == null) return NotFound();

        spot.Name = updateSpotDto.Name ?? spot.Name;
        spot.Description = updateSpotDto.Description ?? spot.Description;
        spot.Latitude = updateSpotDto.Latitude ?? spot.Latitude;
        spot.Longitude = updateSpotDto.Longitude ?? spot.Longitude;
        spot.ImageUrl = updateSpotDto.ImageUrl ?? spot.ImageUrl;

        await _unitOfWork.FishingSpots.UpdateAsync(spot);
        await _unitOfWork.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFishingSpot(int id)
    {
        if (!await _unitOfWork.FishingSpots.ExistsAsync(id)) return NotFound();
        
        await _unitOfWork.FishingSpots.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
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
