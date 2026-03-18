using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Database.Repositories;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly ReviewRepository _reviewRepository;
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IOutputCacheStore _cacheStore;

    public ReviewsController(ReviewRepository reviewRepository, IRepository<FishingSpot> spotRepository, IOutputCacheStore cacheStore)
    {
        _reviewRepository = reviewRepository;
        _spotRepository = spotRepository;
        _cacheStore = cacheStore;
    }

    [HttpGet("spot/{fishingSpotId}")]
    [OutputCache(PolicyName = "ShortCache", Tags = ["reviews"])]
    public async Task<ActionResult<IEnumerable<ReviewDto>>> GetSpotReviews(int fishingSpotId)
    {
        var spot = await _spotRepository.GetByIdAsync(fishingSpotId);
        if (spot == null) return NotFound("Fishing spot not found");

        var reviews = await _reviewRepository.GetByFishingSpotIdAsync(fishingSpotId);
        return Ok(reviews.Select(MapToDto));
    }

    [HttpGet("spot/{fishingSpotId}/average")]
    [OutputCache(PolicyName = "ShortCache", Tags = ["reviews"])]
    public async Task<ActionResult<object>> GetAverageRating(int fishingSpotId)
    {
        var spot = await _spotRepository.GetByIdAsync(fishingSpotId);
        if (spot == null) return NotFound("Fishing spot not found");

        var average = await _reviewRepository.GetAverageRatingAsync(fishingSpotId);
        var count = await _reviewRepository.CountAsync(r => r.FishingSpotId == fishingSpotId);

        return Ok(new { averageRating = average, totalReviews = count });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ReviewDto>> GetReview(int id)
    {
        var review = await _reviewRepository.GetByIdAsync(id);
        return review == null ? NotFound() : Ok(MapToDto(review));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ReviewDto>> CreateReview(CreateReviewDto createReviewDto)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var spot = await _spotRepository.GetByIdAsync(createReviewDto.FishingSpotId);
        if (spot == null) return NotFound("Fishing spot not found");

        // Check if user already has a review for this spot
        var existingReview = await _reviewRepository.GetByUserAndSpotAsync(userId.Value, createReviewDto.FishingSpotId);
        if (existingReview != null)
            return BadRequest("You have already reviewed this fishing spot. You can edit your existing review.");

        // Validate rating
        if (createReviewDto.Rating < 1 || createReviewDto.Rating > 5)
            return BadRequest("Rating must be between 1 and 5");

        var review = new Review
        {
            FishingSpotId = createReviewDto.FishingSpotId,
            UserId = userId.Value,
            Rating = createReviewDto.Rating,
            Comment = createReviewDto.Comment
        };

        await _reviewRepository.AddAsync(review);

        await _cacheStore.EvictByTagAsync("reviews", default);

        // Reload with User included
        var createdReview = await _reviewRepository.GetByIdAsync(review.Id);
        return CreatedAtAction(nameof(GetReview), new { id = review.Id }, MapToDto(createdReview!));
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateReview(int id, UpdateReviewDto updateReviewDto)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var review = await _reviewRepository.GetByIdAsync(id);
        if (review == null) return NotFound();

        // Check if the user owns this review or is admin
        if (review.UserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        if (updateReviewDto.Rating.HasValue)
        {
            if (updateReviewDto.Rating < 1 || updateReviewDto.Rating > 5)
                return BadRequest("Rating must be between 1 and 5");
            review.Rating = updateReviewDto.Rating.Value;
        }

        if (updateReviewDto.Comment != null)
            review.Comment = updateReviewDto.Comment;

        await _reviewRepository.UpdateAsync(review);
        await _cacheStore.EvictByTagAsync("reviews", default);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteReview(int id)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var review = await _reviewRepository.GetByIdAsync(id);
        if (review == null) return NotFound();

        // Check if the user owns this review or is admin
        if (review.UserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        await _reviewRepository.DeleteAsync(id);
        await _cacheStore.EvictByTagAsync("reviews", default);
        return NoContent();
    }

    private static ReviewDto MapToDto(Review review) => new()
    {
        Id = review.Id,
        FishingSpotId = review.FishingSpotId,
        UserId = review.UserId,
        Username = review.User?.Username ?? "Unknown",
        UserProfilePictureUrl = review.User?.ProfilePictureUrl,
        Rating = review.Rating,
        Comment = review.Comment,
        CreatedAt = review.CreatedAt
    };
}
