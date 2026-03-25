using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatchesController : ControllerBase
{
    private readonly IRepository<Catch> _catchRepository;

    public CatchesController(IRepository<Catch> catchRepository)
    {
        _catchRepository = catchRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CatchDto>>> GetCatches()
    {
        var catches = await _catchRepository.GetAllAsync();
        return Ok(catches.Select(MapToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CatchDto>> GetCatch(int id)
    {
        var catchEntity = await _catchRepository.GetByIdAsync(id);
        return catchEntity == null ? NotFound() : Ok(MapToDto(catchEntity));
    }

    [HttpGet("spot/{spotId}/species")]
    public async Task<ActionResult<IEnumerable<string>>> GetSpotSpecies(int spotId)
    {
        var catches = await _catchRepository.FindAsync(c => c.FishingSpotId == spotId);
        var species = catches
            .Select(c => c.FishSpecies.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
        return Ok(species);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<CatchDto>> CreateCatch([FromBody] CreateCatchDto createCatchDto)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(createCatchDto.FishSpecies))
            return BadRequest("Fish species is required.");

        if (createCatchDto.FishingSpotId <= 0)
            return BadRequest("Fishing spot is required.");

        var catchEntity = new Catch
        {
            FishSpecies = createCatchDto.FishSpecies,
            Weight = createCatchDto.Weight,
            Length = createCatchDto.Length,
            CaughtAt = createCatchDto.CaughtAt,
            ImageUrl = createCatchDto.ImageUrl,
            Notes = createCatchDto.Notes,
            UserId = userId.Value,
            FishingSpotId = createCatchDto.FishingSpotId
        };

        await _catchRepository.AddAsync(catchEntity);
        return CreatedAtAction(nameof(GetCatch), new { id = catchEntity.Id }, MapToDto(catchEntity));
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateCatch(int id, [FromBody] UpdateCatchDto updateCatchDto)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var catchEntity = await _catchRepository.GetByIdAsync(id);
        if (catchEntity == null) return NotFound();

        if (catchEntity.UserId != userId.Value && !User.IsInRole(Roles.Admin))
            return Forbid();

        catchEntity.FishSpecies = updateCatchDto.FishSpecies ?? catchEntity.FishSpecies;
        catchEntity.Weight = updateCatchDto.Weight ?? catchEntity.Weight;
        catchEntity.Length = updateCatchDto.Length ?? catchEntity.Length;
        catchEntity.CaughtAt = updateCatchDto.CaughtAt ?? catchEntity.CaughtAt;
        catchEntity.ImageUrl = updateCatchDto.ImageUrl ?? catchEntity.ImageUrl;
        catchEntity.Notes = updateCatchDto.Notes ?? catchEntity.Notes;

        await _catchRepository.UpdateAsync(catchEntity);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteCatch(int id)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var catchEntity = await _catchRepository.GetByIdAsync(id);
        if (catchEntity == null) return NotFound();

        if (catchEntity.UserId != userId.Value && !User.IsInRole(Roles.Admin))
            return Forbid();
        
        await _catchRepository.DeleteAsync(id);
        return NoContent();
    }

    private static CatchDto MapToDto(Catch c) => new()
    {
        Id = c.Id,
        FishSpecies = c.FishSpecies,
        Weight = c.Weight,
        Length = c.Length,
        CaughtAt = c.CaughtAt,
        ImageUrl = c.ImageUrl,
        Notes = c.Notes,
        UserId = c.UserId,
        FishingSpotId = c.FishingSpotId,
        CreatedAt = c.CreatedAt
    };
}
