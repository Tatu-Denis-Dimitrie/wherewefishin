using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Database.Repositories;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PontoonsController : ControllerBase
{
    private readonly PontoonRepository _pontoonRepository;
    private readonly IRepository<FishingSpot> _spotRepository;

    public PontoonsController(PontoonRepository pontoonRepository, IRepository<FishingSpot> spotRepository)
    {
        _pontoonRepository = pontoonRepository;
        _spotRepository = spotRepository;
    }

    [HttpGet("spot/{fishingSpotId}")]
    public async Task<ActionResult<IEnumerable<PontoonDto>>> GetSpotPontoons(int fishingSpotId)
    {
        var spot = await _spotRepository.GetByIdAsync(fishingSpotId);
        if (spot == null) return NotFound("Fishing spot not found");

        var pontoons = await _pontoonRepository.GetByFishingSpotIdAsync(fishingSpotId);
        return Ok(pontoons.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PontoonDto>> GetPontoon(int id)
    {
        var pontoon = await _pontoonRepository.GetByIdAsync(id);
        return pontoon == null ? NotFound() : Ok(MapToDto(pontoon));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<PontoonDto>> CreatePontoon(CreatePontoonDto createPontoonDto)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var spot = await _spotRepository.GetByIdAsync(createPontoonDto.FishingSpotId);
        if (spot == null) return NotFound("Fishing spot not found");

        // Check if user is admin or manages this spot
        if (!User.IsInRole("Admin") && spot.ManagerId != userId && spot.UserId != userId)
            return Forbid("You don't have permission to add pontoons to this fishing spot");

        var pontoon = new Pontoon
        {
            FishingSpotId = createPontoonDto.FishingSpotId,
            Name = createPontoonDto.Name,
            SouthWestLat = createPontoonDto.SouthWestLat,
            SouthWestLng = createPontoonDto.SouthWestLng,
            NorthEastLat = createPontoonDto.NorthEastLat,
            NorthEastLng = createPontoonDto.NorthEastLng,
            Color = createPontoonDto.Color ?? "#3388ff"
        };

        await _pontoonRepository.AddAsync(pontoon);
        return CreatedAtAction(nameof(GetPontoon), new { id = pontoon.Id }, MapToDto(pontoon));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UpdatePontoon(int id, UpdatePontoonDto updatePontoonDto)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var pontoon = await _pontoonRepository.GetByIdAsync(id);
        if (pontoon == null) return NotFound();

        var spot = await _spotRepository.GetByIdAsync(pontoon.FishingSpotId);
        if (spot == null) return NotFound("Fishing spot not found");

        // Check if user is admin or manages this spot
        if (!User.IsInRole("Admin") && spot.ManagerId != userId && spot.UserId != userId)
            return Forbid("You don't have permission to edit pontoons on this fishing spot");

        if (updatePontoonDto.Name != null)
            pontoon.Name = updatePontoonDto.Name;
        if (updatePontoonDto.SouthWestLat.HasValue)
            pontoon.SouthWestLat = updatePontoonDto.SouthWestLat.Value;
        if (updatePontoonDto.SouthWestLng.HasValue)
            pontoon.SouthWestLng = updatePontoonDto.SouthWestLng.Value;
        if (updatePontoonDto.NorthEastLat.HasValue)
            pontoon.NorthEastLat = updatePontoonDto.NorthEastLat.Value;
        if (updatePontoonDto.NorthEastLng.HasValue)
            pontoon.NorthEastLng = updatePontoonDto.NorthEastLng.Value;
        if (updatePontoonDto.Color != null)
            pontoon.Color = updatePontoonDto.Color;

        await _pontoonRepository.UpdateAsync(pontoon);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeletePontoon(int id)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var pontoon = await _pontoonRepository.GetByIdAsync(id);
        if (pontoon == null) return NotFound();

        var spot = await _spotRepository.GetByIdAsync(pontoon.FishingSpotId);
        if (spot == null) return NotFound("Fishing spot not found");

        // Check if user is admin or manages this spot
        if (!User.IsInRole("Admin") && spot.ManagerId != userId && spot.UserId != userId)
            return Forbid("You don't have permission to delete pontoons from this fishing spot");

        await _pontoonRepository.DeleteAsync(id);
        return NoContent();
    }

    private static PontoonDto MapToDto(Pontoon pontoon) => new()
    {
        Id = pontoon.Id,
        FishingSpotId = pontoon.FishingSpotId,
        Name = pontoon.Name,
        SouthWestLat = pontoon.SouthWestLat,
        SouthWestLng = pontoon.SouthWestLng,
        NorthEastLat = pontoon.NorthEastLat,
        NorthEastLng = pontoon.NorthEastLng,
        Color = pontoon.Color,
        CreatedAt = pontoon.CreatedAt
    };
}
