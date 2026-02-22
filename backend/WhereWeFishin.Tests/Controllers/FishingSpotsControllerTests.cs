using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.Tests.Controllers;

public class FishingSpotsControllerTests
{
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly FishingSpotsController _controller;

    public FishingSpotsControllerTests()
    {
        _spotRepository = Substitute.For<IRepository<FishingSpot>>();
        _controller = new FishingSpotsController(_spotRepository);
    }

    private static FishingSpot CreateSpot(int id = 1, string name = "Lake Spot") => new()
    {
        Id = id,
        Name = name,
        Description = "Nice place",
        Latitude = 45.0,
        Longitude = 25.0,
        PricePerHour = 10m,
        UserId = 1
    };


    [Fact]
    public async Task GetFishingSpots_ReturnsAllSpots()
    {
        // Arrange
        var spots = new List<FishingSpot> { CreateSpot(1, "River"), CreateSpot(2, "Lake") };
        _spotRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(spots);

        // Act
        var result = await _controller.GetFishingSpots();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSpots = Assert.IsAssignableFrom<IEnumerable<FishingSpotDto>>(okResult.Value);
        Assert.Equal(2, returnedSpots.Count());
    }

    [Fact]
    public async Task GetFishingSpots_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        _spotRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<FishingSpot>());

        // Act
        var result = await _controller.GetFishingSpots();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSpots = Assert.IsAssignableFrom<IEnumerable<FishingSpotDto>>(okResult.Value);
        Assert.Empty(returnedSpots);
    }


    [Fact]
    public async Task GetFishingSpot_WithValidId_ReturnsSpot()
    {
        // Arrange
        var spot = CreateSpot(1, "River Bank");
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        // Act
        var result = await _controller.GetFishingSpot(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedSpot = Assert.IsType<FishingSpotDto>(okResult.Value);
        Assert.Equal("River Bank", returnedSpot.Name);
        Assert.Equal(45.0, returnedSpot.Latitude);
        Assert.Equal(10m, returnedSpot.PricePerHour);
    }

    [Fact]
    public async Task GetFishingSpot_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _spotRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((FishingSpot?)null);

        // Act
        var result = await _controller.GetFishingSpot(999);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }


    [Fact]
    public async Task CreateFishingSpot_WithValidData_ReturnsCreated()
    {
        // Arrange
        var createDto = new CreateFishingSpotDto
        {
            Name = "New Spot",
            Description = "A great location",
            Latitude = 46.5,
            Longitude = 26.5,
            PricePerHour = 15m,
            UserId = 1
        };
        _spotRepository.AddAsync(Arg.Any<FishingSpot>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<FishingSpot>());

        // Act
        var result = await _controller.CreateFishingSpot(createDto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedSpot = Assert.IsType<FishingSpotDto>(createdResult.Value);
        Assert.Equal("New Spot", returnedSpot.Name);
        Assert.Equal(46.5, returnedSpot.Latitude);
        Assert.Equal(15m, returnedSpot.PricePerHour);
    }

    [Fact]
    public async Task CreateFishingSpot_CallsAddAsync()
    {
        // Arrange
        var createDto = new CreateFishingSpotDto { Name = "Spot", Latitude = 1, Longitude = 1, UserId = 1 };
        _spotRepository.AddAsync(Arg.Any<FishingSpot>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<FishingSpot>());

        // Act
        await _controller.CreateFishingSpot(createDto);

        // Assert
        await _spotRepository.Received(1).AddAsync(Arg.Any<FishingSpot>(), Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task UpdateFishingSpot_WithValidData_ReturnsNoContent()
    {
        // Arrange
        var spot = CreateSpot(1, "Old Name");
        var updateDto = new UpdateFishingSpotDto { Name = "Updated Name", PricePerHour = 20m };
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        // Act
        var result = await _controller.UpdateFishingSpot(1, updateDto);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _spotRepository.Received(1).UpdateAsync(Arg.Any<FishingSpot>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateFishingSpot_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _spotRepository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((FishingSpot?)null);
        var updateDto = new UpdateFishingSpotDto { Name = "Updated" };

        // Act
        var result = await _controller.UpdateFishingSpot(999, updateDto);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateFishingSpot_UpdatesOnlyProvidedFields()
    {
        // Arrange
        var spot = CreateSpot(1, "Original Name");
        spot.Description = "Original Description";
        var updateDto = new UpdateFishingSpotDto { Name = "New Name" };
        _spotRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(spot);

        // Act
        await _controller.UpdateFishingSpot(1, updateDto);

        // Assert – description should remain unchanged
        await _spotRepository.Received(1).UpdateAsync(
            Arg.Is<FishingSpot>(s => s.Name == "New Name" && s.Description == "Original Description"),
            Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task DeleteFishingSpot_WithValidId_ReturnsNoContent()
    {
        // Arrange
        _spotRepository.ExistsAsync(1, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _controller.DeleteFishingSpot(1);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _spotRepository.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteFishingSpot_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        _spotRepository.ExistsAsync(999, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _controller.DeleteFishingSpot(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
        await _spotRepository.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
