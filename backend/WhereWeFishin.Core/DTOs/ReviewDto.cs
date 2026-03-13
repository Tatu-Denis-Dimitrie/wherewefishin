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
    public int FishingSpotId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class UpdateReviewDto
{
    public int? Rating { get; set; }
    public string? Comment { get; set; }
}
