using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using NSubstitute;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Tests.TestHelpers;

namespace WhereWeFishin.Tests.Controllers;

public class StockingsControllerTests
{
    private readonly IRepository<FishStocking> _stockingRepository;
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IOutputCacheStore _cacheStore;
    private readonly StockingsController _controller;
    private readonly List<FishStocking> _stockings;
    private readonly List<FishingSpot> _spots;

    public StockingsControllerTests()
    {
        _stockingRepository = Substitute.For<IRepository<FishStocking>>();
        _spotRepository = Substitute.For<IRepository<FishingSpot>>();
        _cacheStore = Substitute.For<IOutputCacheStore>();
        _stockings = _stockingRepository.UseInMemoryStore<FishStocking>();
        _spots = _spotRepository.UseInMemoryStore<FishingSpot>();

        _controller = new StockingsController(_stockingRepository, _spotRepository, _cacheStore);
        SetUser(1);
    }

    private void SetUser(int userId)
    {
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId);
    }

    private FishingSpot AddSpot(int id = 1, int userId = 1)
    {
        var spot = new FishingSpot
        {
            Id = id,
            Name = $"Spot {id}",
            Latitude = 45,
            Longitude = 25,
            PricePerHour = 10,
            UserId = userId
        };
        _spots.Add(spot);
        return spot;
    }

    private FishStocking AddStocking(int id, int spotId, DateTime stockingDate, string species = "Carp", int quantity = 100) 
    {
        var stocking = new FishStocking
        {
            Id = id,
            FishingSpotId = spotId,
            StockingDate = stockingDate,
            Species = species,
            Quantity = quantity,
            Notes = "Seeded"
        };
        _stockings.Add(stocking);
        return stocking;
    }

    [Fact]
    public async Task GetStockings_ReturnsItemsOrderedByDateDescending()
    {
        // Arrange
        AddStocking(1, 1, DateTime.UtcNow.AddDays(-3), species: "Pike");
        AddStocking(2, 1, DateTime.UtcNow.AddDays(-1), species: "Carp");
        AddStocking(3, 2, DateTime.UtcNow.AddDays(-2), species: "Perch");

        // Act
        var result = await _controller.GetStockings(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var stockings = Assert.IsAssignableFrom<IEnumerable<FishStockingDto>>(okResult.Value).ToList();
        Assert.Equal(2, stockings.Count);
        Assert.Equal(2, stockings[0].Id);
        Assert.Equal(1, stockings[1].Id);
    }

    [Fact]
    public async Task CreateStocking_WhenSpotMissing_ReturnsNotFound()
    {
        // Act
        var result = await _controller.CreateStocking(999, new CreateFishStockingDto
        {
            StockingDate = DateTime.UtcNow,
            Species = "Carp",
            Quantity = 100
        });

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateStocking_WhenUserCannotManageSpot_ReturnsForbid()
    {
        // Arrange
        AddSpot(1, userId: 10);
        SetUser(99);

        // Act
        var result = await _controller.CreateStocking(1, new CreateFishStockingDto
        {
            StockingDate = DateTime.UtcNow,
            Species = "Carp",
            Quantity = 100
        });

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task CreateStocking_WithValidData_ReturnsCreatedAndEvictsCache()
    {
        // Arrange
        AddSpot(1, userId: 1);

        // Act
        var result = await _controller.CreateStocking(1, new CreateFishStockingDto
        {
            StockingDate = DateTime.UtcNow,
            Species = "Catfish",
            Quantity = 150,
            Notes = "Fresh stocking"
        });

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<FishStockingDto>(createdResult.Value);
        Assert.True(dto.Id > 0);
        Assert.Equal("Catfish", dto.Species);
        Assert.Equal(150, dto.Quantity);
        await _cacheStore.Received(1).EvictByTagAsync("stockings", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStocking_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        AddSpot(1);

        // Act
        var result = await _controller.UpdateStocking(1, 999, new UpdateFishStockingDto { Quantity = 50 });

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateStocking_WhenSpotMissing_ReturnsNotFound()
    {
        // Arrange
        AddStocking(1, 1, DateTime.UtcNow);

        // Act
        var result = await _controller.UpdateStocking(1, 1, new UpdateFishStockingDto { Quantity = 50 });

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateStocking_WhenUserCannotManageSpot_ReturnsForbid()
    {
        // Arrange
        AddSpot(1, userId: 10);
        AddStocking(1, 1, DateTime.UtcNow);
        SetUser(99);

        // Act
        var result = await _controller.UpdateStocking(1, 1, new UpdateFishStockingDto { Quantity = 50 });

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateStocking_WithValidData_UpdatesFieldsAndEvictsCache()
    {
        // Arrange
        AddSpot(1, userId: 1);
        AddStocking(1, 1, DateTime.UtcNow.AddDays(-1), species: "Old", quantity: 10);

        // Act
        var result = await _controller.UpdateStocking(1, 1, new UpdateFishStockingDto
        {
            Species = "New Species",
            Quantity = 999,
            Notes = "Updated"
        });

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _stockingRepository.Received(1).UpdateAsync(
            Arg.Is<FishStocking>(stocking =>
                stocking.Species == "New Species" &&
                stocking.Quantity == 999 &&
                stocking.Notes == "Updated"),
            Arg.Any<CancellationToken>());
        await _cacheStore.Received(1).EvictByTagAsync("stockings", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteStocking_WhenMissing_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteStocking(1, 999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteStocking_WhenSpotMissing_ReturnsNotFound()
    {
        // Arrange
        AddStocking(1, 1, DateTime.UtcNow);

        // Act
        var result = await _controller.DeleteStocking(1, 1);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteStocking_WhenUserCannotManageSpot_ReturnsForbid()
    {
        // Arrange
        AddSpot(1, userId: 10);
        AddStocking(1, 1, DateTime.UtcNow);
        SetUser(99);

        // Act
        var result = await _controller.DeleteStocking(1, 1);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DeleteStocking_WithValidData_DeletesAndEvictsCache()
    {
        // Arrange
        AddSpot(1, userId: 1);
        AddStocking(1, 1, DateTime.UtcNow);

        // Act
        var result = await _controller.DeleteStocking(1, 1);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _stockingRepository.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
        await _cacheStore.Received(1).EvictByTagAsync("stockings", Arg.Any<CancellationToken>());
    }
}