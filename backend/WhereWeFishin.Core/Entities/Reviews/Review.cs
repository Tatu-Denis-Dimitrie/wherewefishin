namespace WhereWeFishin.Core.Entities;

public class Review : BaseEntity
{
    public int FishingSpotId { get; set; }
    public int UserId { get; set; }
    public int Rating { get; set; } // 1-5 stars
    public string? Comment { get; set; }

    public FishingSpot FishingSpot { get; set; } = null!;
    public User User { get; set; } = null!;
}
