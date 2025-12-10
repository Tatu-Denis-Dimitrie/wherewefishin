using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
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

    [HttpPost]
    public async Task<ActionResult<CatchDto>> CreateCatch(CreateCatchDto createCatchDto)
    {
        var catchEntity = new Catch
        {
            FishSpecies = createCatchDto.FishSpecies,
            Weight = createCatchDto.Weight,
            Length = createCatchDto.Length,
            CaughtAt = createCatchDto.CaughtAt,
            ImageUrl = createCatchDto.ImageUrl,
            Notes = createCatchDto.Notes,
            UserId = 1, 
            FishingSpotId = createCatchDto.FishingSpotId
        };

        await _catchRepository.AddAsync(catchEntity);
        return CreatedAtAction(nameof(GetCatch), new { id = catchEntity.Id }, MapToDto(catchEntity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCatch(int id, UpdateCatchDto updateCatchDto)
    {
        var catchEntity = await _catchRepository.GetByIdAsync(id);
        if (catchEntity == null) return NotFound();

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
    public async Task<IActionResult> DeleteCatch(int id)
    {
        if (!await _catchRepository.ExistsAsync(id)) return NotFound();
        
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
