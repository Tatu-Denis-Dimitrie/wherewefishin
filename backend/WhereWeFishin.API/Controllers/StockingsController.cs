using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/fishingspots/{spotId}/stockings")]
[Authorize]
public class StockingsController : ControllerBase
{
    private readonly IRepository<FishStocking> _stockingRepository;
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IOutputCacheStore _cacheStore;

    public StockingsController(
        IRepository<FishStocking> stockingRepository,
        IRepository<FishingSpot> spotRepository,
        IOutputCacheStore cacheStore)
    {
        _stockingRepository = stockingRepository;
        _spotRepository = spotRepository;
        _cacheStore = cacheStore;
    }

    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = "MediumCache", Tags = ["stockings"])]
    public async Task<ActionResult<IEnumerable<FishStockingDto>>> GetStockings(int spotId)
    {
        var stockings = await _stockingRepository.FindAsync(s => s.FishingSpotId == spotId);
        return Ok(stockings.OrderByDescending(s => s.StockingDate).Select(MapToDto));
    }

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<ActionResult<FishStockingDto>> CreateStocking(int spotId, CreateFishStockingDto dto)
    {
        var spot = await _spotRepository.GetByIdAsync(spotId);
        if (spot == null) return NotFound("Fishing spot not found");

        var userId = User.GetUserId();
        if (!User.IsInRole(Roles.Admin) && spot.ManagerId != userId && spot.UserId != userId)
            return Forbid();

        var stocking = new FishStocking
        {
            FishingSpotId = spotId,
            StockingDate = dto.StockingDate,
            Species = dto.Species,
            Quantity = dto.Quantity,
            Notes = dto.Notes
        };

        await _stockingRepository.AddAsync(stocking);
        await _cacheStore.EvictByTagAsync("stockings", default);
        return CreatedAtAction(nameof(GetStockings), new { spotId }, MapToDto(stocking));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> UpdateStocking(int spotId, int id, UpdateFishStockingDto dto)
    {
        var stocking = await _stockingRepository.GetByIdAsync(id);
        if (stocking == null || stocking.FishingSpotId != spotId) return NotFound();

        var spot = await _spotRepository.GetByIdAsync(spotId);
        if (spot == null) return NotFound();

        var userId = User.GetUserId();
        if (!User.IsInRole(Roles.Admin) && spot.ManagerId != userId && spot.UserId != userId)
            return Forbid();

        if (dto.StockingDate.HasValue) stocking.StockingDate = dto.StockingDate.Value;
        if (dto.Species != null) stocking.Species = dto.Species;
        if (dto.Quantity.HasValue) stocking.Quantity = dto.Quantity.Value;
        if (dto.Notes != null) stocking.Notes = dto.Notes;

        await _stockingRepository.UpdateAsync(stocking);
        await _cacheStore.EvictByTagAsync("stockings", default);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> DeleteStocking(int spotId, int id)
    {
        var stocking = await _stockingRepository.GetByIdAsync(id);
        if (stocking == null || stocking.FishingSpotId != spotId) return NotFound();

        var spot = await _spotRepository.GetByIdAsync(spotId);
        if (spot == null) return NotFound();

        var userId = User.GetUserId();
        if (!User.IsInRole(Roles.Admin) && spot.ManagerId != userId && spot.UserId != userId)
            return Forbid();

        await _stockingRepository.DeleteAsync(id);
        await _cacheStore.EvictByTagAsync("stockings", default);
        return NoContent();
    }

    private static FishStockingDto MapToDto(FishStocking s) => new()
    {
        Id = s.Id,
        FishingSpotId = s.FishingSpotId,
        StockingDate = s.StockingDate,
        Species = s.Species,
        Quantity = s.Quantity,
        Notes = s.Notes,
        CreatedAt = s.CreatedAt
    };
}
