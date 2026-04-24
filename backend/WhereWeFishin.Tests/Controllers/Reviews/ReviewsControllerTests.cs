using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Database.Context;
using WhereWeFishin.Database.Repositories;
using WhereWeFishin.Tests.TestHelpers;

namespace WhereWeFishin.Tests.Controllers;

public class ReviewsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ReviewRepository _reviewRepository;
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IOutputCacheStore _cacheStore;
    private readonly ReviewsController _controller;

    public ReviewsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _reviewRepository = new ReviewRepository(_context);
        _spotRepository = new Repository<FishingSpot>(_context);
        _cacheStore = Substitute.For<IOutputCacheStore>();
        _controller = new ReviewsController(_reviewRepository, _spotRepository, _cacheStore);

        SeedBaseData();
    }

    private void SeedBaseData()
    {
        _context.Users.AddRange(
            new User { Id = 1, Username = "owner", Email = "owner@test.com", PasswordHash = "hash", Role = UserRole.User },
            new User { Id = 2, Username = "reviewer", Email = "reviewer@test.com", PasswordHash = "hash", Role = UserRole.User },
            new User { Id = 3, Username = "otheruser", Email = "other@test.com", PasswordHash = "hash", Role = UserRole.User },
            new User { Id = 99, Username = "admin", Email = "admin@test.com", PasswordHash = "hash", Role = UserRole.Admin });

        _context.FishingSpots.Add(new FishingSpot
        {
            Id = 1,
            Name = "Delta Lake",
            Latitude = 45.0,
            Longitude = 25.0,
            UserId = 1,
            PricePerHour = 10m
        });

        _context.SaveChanges();
    }

    private void SetUser(int userId, string role = Roles.User)
    {
        ControllerContextFactory.SetAuthenticatedUser(
            _controller,
            userId,
            role,
            username: userId == 99 ? "admin" : $"user{userId}",
            email: userId == 99 ? "admin@test.com" : $"user{userId}@test.com");
    }

    private async Task<Review> AddReviewAsync(
        int id,
        int userId,
        int rating,
        string? comment = null,
        DateTime? createdAt = null,
        bool isDeleted = false)
    {
        var review = new Review
        {
            Id = id,
            FishingSpotId = 1,
            UserId = userId,
            Rating = rating,
            Comment = comment,
            IsDeleted = isDeleted
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        if (createdAt.HasValue)
        {
            review.CreatedAt = createdAt.Value;
            await _context.SaveChangesAsync();
        }

        return review;
    }

    private static T GetAnonymousProperty<T>(object instance, string propertyName)
        => (T)instance.GetType().GetProperty(propertyName)!.GetValue(instance)!;

    [Fact]
    public async Task GetSpotReviews_WhenSpotMissing_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetSpotReviews(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSpotReviews_ReturnsReviewsOrderedByCreatedAtDescending()
    {
        // Arrange
        await AddReviewAsync(1, userId: 2, rating: 5, comment: "Great", createdAt: DateTime.UtcNow.AddHours(-2));
        await AddReviewAsync(2, userId: 3, rating: 3, comment: "Ok", createdAt: DateTime.UtcNow.AddHours(-1));

        // Act
        var result = await _controller.GetSpotReviews(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var reviews = Assert.IsAssignableFrom<IEnumerable<ReviewDto>>(okResult.Value).ToList();
        Assert.Equal(2, reviews.Count);
        Assert.Equal(2, reviews[0].Id);
        Assert.Equal("otheruser", reviews[0].Username);
        Assert.Equal(1, reviews[1].Id);
        Assert.Equal("reviewer", reviews[1].Username);
    }

    [Fact]
    public async Task GetAverageRating_WhenSpotMissing_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetAverageRating(999);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAverageRating_WithReviews_ReturnsAverageAndCount()
    {
        // Arrange
        await AddReviewAsync(1, userId: 2, rating: 5);
        await AddReviewAsync(2, userId: 3, rating: 3);

        // Act
        var result = await _controller.GetAverageRating(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(4d, GetAnonymousProperty<double>(okResult.Value!, "averageRating"));
        Assert.Equal(2, GetAnonymousProperty<int>(okResult.Value!, "totalReviews"));
    }

    [Fact]
    public async Task GetAverageRating_WithoutReviews_ReturnsNullAverageAndZeroCount()
    {
        // Act
        var result = await _controller.GetAverageRating(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Null(GetAnonymousProperty<object?>(okResult.Value!, "averageRating"));
        Assert.Equal(0, GetAnonymousProperty<int>(okResult.Value!, "totalReviews"));
    }

    [Fact]
    public async Task GetReview_WhenFound_ReturnsMappedReview()
    {
        // Arrange
        await AddReviewAsync(10, userId: 2, rating: 4, comment: "Solid place");

        // Act
        var result = await _controller.GetReview(10);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var review = Assert.IsType<ReviewDto>(okResult.Value);
        Assert.Equal(10, review.Id);
        Assert.Equal("reviewer", review.Username);
        Assert.Equal(4, review.Rating);
    }

    [Fact]
    public async Task GetReview_WhenMissing_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetReview(404);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task CreateReview_WhenAnonymous_ReturnsUnauthorized()
    {
        // Arrange
        ControllerContextFactory.SetAnonymousUser(_controller);

        // Act
        var result = await _controller.CreateReview(new CreateReviewDto { FishingSpotId = 1, Rating = 5 });

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task CreateReview_WhenSpotMissing_ReturnsNotFound()
    {
        // Arrange
        SetUser(2);

        // Act
        var result = await _controller.CreateReview(new CreateReviewDto { FishingSpotId = 999, Rating = 5 });

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateReview_WhenDuplicateExists_ReturnsBadRequest()
    {
        // Arrange
        SetUser(2);
        await AddReviewAsync(1, userId: 2, rating: 4);

        // Act
        var result = await _controller.CreateReview(new CreateReviewDto { FishingSpotId = 1, Rating = 5 });

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateReview_WhenRatingIsOutOfRange_ReturnsBadRequest()
    {
        // Arrange
        SetUser(2);

        // Act
        var result = await _controller.CreateReview(new CreateReviewDto { FishingSpotId = 1, Rating = 6 });

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateReview_WithValidData_PersistsReviewAndEvictsCache()
    {
        // Arrange
        SetUser(2);

        // Act
        var result = await _controller.CreateReview(new CreateReviewDto
        {
            FishingSpotId = 1,
            Rating = 5,
            Comment = "Excellent"
        });

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var review = Assert.IsType<ReviewDto>(createdResult.Value);
        Assert.True(review.Id > 0);
        Assert.Equal(2, review.UserId);
        Assert.Equal("reviewer", review.Username);
        Assert.Equal("Excellent", review.Comment);
        await _cacheStore.Received(1).EvictByTagAsync("reviews", Arg.Any<CancellationToken>());
        Assert.Equal(1, await _context.Reviews.CountAsync());
    }

    [Fact]
    public async Task UpdateReview_WhenAnonymous_ReturnsUnauthorized()
    {
        // Arrange
        ControllerContextFactory.SetAnonymousUser(_controller);

        // Act
        var result = await _controller.UpdateReview(1, new UpdateReviewDto { Rating = 4 });

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task UpdateReview_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        SetUser(2);

        // Act
        var result = await _controller.UpdateReview(999, new UpdateReviewDto { Rating = 4 });

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateReview_WhenUserIsNotOwnerAndNotAdmin_ReturnsForbid()
    {
        // Arrange
        SetUser(3);
        await AddReviewAsync(1, userId: 2, rating: 4);

        // Act
        var result = await _controller.UpdateReview(1, new UpdateReviewDto { Rating = 5 });

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateReview_WhenRatingIsInvalid_ReturnsBadRequest()
    {
        // Arrange
        SetUser(2);
        await AddReviewAsync(1, userId: 2, rating: 4);

        // Act
        var result = await _controller.UpdateReview(1, new UpdateReviewDto { Rating = 0 });

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateReview_WhenOwner_UpdatesReviewAndEvictsCache()
    {
        // Arrange
        SetUser(2);
        await AddReviewAsync(1, userId: 2, rating: 4, comment: "Initial");

        // Act
        var result = await _controller.UpdateReview(1, new UpdateReviewDto
        {
            Rating = 5,
            Comment = "Updated"
        });

        // Assert
        Assert.IsType<NoContentResult>(result);
        var updatedReview = await _reviewRepository.GetByIdAsync(1);
        Assert.NotNull(updatedReview);
        Assert.Equal(5, updatedReview.Rating);
        Assert.Equal("Updated", updatedReview.Comment);
        await _cacheStore.Received(1).EvictByTagAsync("reviews", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateReview_WhenAdmin_CanUpdateOtherUsersReview()
    {
        // Arrange
        SetUser(99, Roles.Admin);
        await AddReviewAsync(1, userId: 2, rating: 4, comment: "Initial");

        // Act
        var result = await _controller.UpdateReview(1, new UpdateReviewDto { Comment = "Admin edit" });

        // Assert
        Assert.IsType<NoContentResult>(result);
        var updatedReview = await _reviewRepository.GetByIdAsync(1);
        Assert.Equal("Admin edit", updatedReview!.Comment);
    }

    [Fact]
    public async Task DeleteReview_WhenAnonymous_ReturnsUnauthorized()
    {
        // Arrange
        ControllerContextFactory.SetAnonymousUser(_controller);

        // Act
        var result = await _controller.DeleteReview(1);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task DeleteReview_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        SetUser(2);

        // Act
        var result = await _controller.DeleteReview(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteReview_WhenUserIsNotOwnerAndNotAdmin_ReturnsForbid()
    {
        // Arrange
        SetUser(3);
        await AddReviewAsync(1, userId: 2, rating: 4);

        // Act
        var result = await _controller.DeleteReview(1);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DeleteReview_WhenOwner_SoftDeletesReviewAndEvictsCache()
    {
        // Arrange
        SetUser(2);
        await AddReviewAsync(1, userId: 2, rating: 4);

        // Act
        var result = await _controller.DeleteReview(1);

        // Assert
        Assert.IsType<NoContentResult>(result);
        var deletedReview = await _context.Reviews.IgnoreQueryFilters().SingleAsync(review => review.Id == 1);
        Assert.True(deletedReview.IsDeleted);
        await _cacheStore.Received(1).EvictByTagAsync("reviews", Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}