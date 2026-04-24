using System.ComponentModel.DataAnnotations;

namespace WhereWeFishin.Core.DTOs;

public class ReviewDto
{
    public int Id { get; set; }
    public int FishingSpotId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? UserProfilePictureUrl { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateReviewDto
{
    [Range(1, int.MaxValue)]
    public int FishingSpotId { get; set; }

    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}

public class UpdateReviewDto
{
    [Range(1, 5)]
    public int? Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}
