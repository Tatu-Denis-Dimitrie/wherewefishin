using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CatchesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CatchesController> _logger;

    public CatchesController(IUnitOfWork unitOfWork, ILogger<CatchesController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CatchDto>>> GetCatches()
    {
        var catches = await _unitOfWork.Catches.GetAllAsync();
        var catchDtos = catches.Select(c => new CatchDto
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
        });

        return Ok(catchDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CatchDto>> GetCatch(Guid id)
    {
        var catchEntity = await _unitOfWork.Catches.GetByIdAsync(id);
        if (catchEntity == null)
        {
            return NotFound();
        }

        var catchDto = new CatchDto
        {
            Id = catchEntity.Id,
            FishSpecies = catchEntity.FishSpecies,
            Weight = catchEntity.Weight,
            Length = catchEntity.Length,
            CaughtAt = catchEntity.CaughtAt,
            ImageUrl = catchEntity.ImageUrl,
            Notes = catchEntity.Notes,
            UserId = catchEntity.UserId,
            FishingSpotId = catchEntity.FishingSpotId,
            CreatedAt = catchEntity.CreatedAt
        };

        return Ok(catchDto);
    }

    [HttpPost]
    public async Task<ActionResult<CatchDto>> CreateCatch(CreateCatchDto createCatchDto)
    {
        // TODO: Get userId from authenticated user
        var userId = Guid.NewGuid(); // Temporary, should come from auth

        var catchEntity = new Catch
        {
            FishSpecies = createCatchDto.FishSpecies,
            Weight = createCatchDto.Weight,
            Length = createCatchDto.Length,
            CaughtAt = createCatchDto.CaughtAt,
            ImageUrl = createCatchDto.ImageUrl,
            Notes = createCatchDto.Notes,
            UserId = userId,
            FishingSpotId = createCatchDto.FishingSpotId
        };

        await _unitOfWork.Catches.AddAsync(catchEntity);
        await _unitOfWork.SaveChangesAsync();

        var catchDto = new CatchDto
        {
            Id = catchEntity.Id,
            FishSpecies = catchEntity.FishSpecies,
            Weight = catchEntity.Weight,
            Length = catchEntity.Length,
            CaughtAt = catchEntity.CaughtAt,
            ImageUrl = catchEntity.ImageUrl,
            Notes = catchEntity.Notes,
            UserId = catchEntity.UserId,
            FishingSpotId = catchEntity.FishingSpotId,
            CreatedAt = catchEntity.CreatedAt
        };

        return CreatedAtAction(nameof(GetCatch), new { id = catchEntity.Id }, catchDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCatch(Guid id, UpdateCatchDto updateCatchDto)
    {
        var catchEntity = await _unitOfWork.Catches.GetByIdAsync(id);
        if (catchEntity == null)
        {
            return NotFound();
        }

        if (updateCatchDto.FishSpecies != null) catchEntity.FishSpecies = updateCatchDto.FishSpecies;
        if (updateCatchDto.Weight.HasValue) catchEntity.Weight = updateCatchDto.Weight;
        if (updateCatchDto.Length.HasValue) catchEntity.Length = updateCatchDto.Length;
        if (updateCatchDto.CaughtAt.HasValue) catchEntity.CaughtAt = updateCatchDto.CaughtAt.Value;
        if (updateCatchDto.ImageUrl != null) catchEntity.ImageUrl = updateCatchDto.ImageUrl;
        if (updateCatchDto.Notes != null) catchEntity.Notes = updateCatchDto.Notes;

        await _unitOfWork.Catches.UpdateAsync(catchEntity);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCatch(Guid id)
    {
        var exists = await _unitOfWork.Catches.ExistsAsync(id);
        if (!exists)
        {
            return NotFound();
        }

        await _unitOfWork.Catches.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }
}
