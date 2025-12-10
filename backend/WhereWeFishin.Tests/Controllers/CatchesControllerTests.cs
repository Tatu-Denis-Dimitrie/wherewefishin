using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.Tests.Controllers;

public class CatchesControllerTests
{
    private readonly IRepository<Catch> _catchRepository;
    private readonly CatchesController _controller;

    public CatchesControllerTests()
    {
        _catchRepository = Substitute.For<IRepository<Catch>>();
        _controller = new CatchesController(_catchRepository);
    }

    [Fact]
    public async Task GetCatches_ReturnsAllCatches()
    {
        // Arrange
        var catches = new List<Catch>
        {
            new Catch 
            { 
                Id = 1, 
                FishSpecies = "Bass", 
                Weight = 2.5, 
                Length = 45.0, 
                CaughtAt = DateTime.Now,
                UserId = 1,
                FishingSpotId = 1
            },
            new Catch 
            { 
                Id = 2, 
                FishSpecies = "Trout", 
                Weight = 1.8, 
                Length = 35.0, 
                CaughtAt = DateTime.Now,
                UserId = 1,
                FishingSpotId = 1
            }
        };
        _catchRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(catches);

        // Act
        var result = await _controller.GetCatches();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedCatches = Assert.IsAssignableFrom<IEnumerable<CatchDto>>(okResult.Value);
        Assert.Equal(2, returnedCatches.Count());
    }

    [Fact]
    public async Task GetCatch_WithValidId_ReturnsCatch()
    {
        // Arrange
        var catchEntity = new Catch
        {
            Id = 1,
            FishSpecies = "Bass",
            Weight = 2.5,
            Length = 45.0,
            CaughtAt = DateTime.Now,
            UserId = 1,
            FishingSpotId = 1
        };
        _catchRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(catchEntity);

        // Act
        var result = await _controller.GetCatch(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedCatch = Assert.IsType<CatchDto>(okResult.Value);
        Assert.Equal("Bass", returnedCatch.FishSpecies);
        Assert.Equal(2.5, returnedCatch.Weight);
    }

    [Fact]
    public async Task CreateCatch_WithValidData_ReturnsCreatedCatch()
    {
        // Arrange
        var createDto = new CreateCatchDto
        {
            FishSpecies = "Pike",
            Weight = 5.0,
            Length = 80.0,
            CaughtAt = DateTime.Now,
            FishingSpotId = 1,
            Notes = "Great catch!"
        };

        _catchRepository.AddAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => 
            {
                var entity = callInfo.Arg<Catch>();
                entity.Id = 1;
                return entity;
            });

        // Act
        var result = await _controller.CreateCatch(createDto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var returnedCatch = Assert.IsType<CatchDto>(createdResult.Value);
        Assert.Equal("Pike", returnedCatch.FishSpecies);
        Assert.Equal(5.0, returnedCatch.Weight);
        await _catchRepository.Received(1).AddAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCatch_WithValidData_ReturnsNoContent()
    {
        // Arrange
        var existingCatch = new Catch
        {
            Id = 1,
            FishSpecies = "Bass",
            Weight = 2.5,
            Length = 45.0,
            CaughtAt = DateTime.Now,
            UserId = 1,
            FishingSpotId = 1
        };
        var updateDto = new UpdateCatchDto
        {
            FishSpecies = "Large Bass",
            Weight = 3.0
        };

        _catchRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(existingCatch);

        // Act
        var result = await _controller.UpdateCatch(1, updateDto);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _catchRepository.Received(1).UpdateAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCatch_WithValidId_ReturnsNoContent()
    {
        // Arrange
        _catchRepository.ExistsAsync(1, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _controller.DeleteCatch(1);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _catchRepository.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
    }
}
