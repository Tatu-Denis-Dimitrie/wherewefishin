namespace WhereWeFishin.Core.Entities;

public class SpotEmployee : BaseEntity
{
    public int UserId { get; set; }
    public int FishingSpotId { get; set; }

    public User User { get; set; } = null!;
    public FishingSpot FishingSpot { get; set; } = null!;
}
