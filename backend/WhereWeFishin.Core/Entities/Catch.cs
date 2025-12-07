namespace WhereWeFishin.Core.Entities;

public class Catch : BaseEntity
{
    public string FishSpecies { get; set; } = string.Empty;
    public double? Weight { get; set; }
    public double? Length { get; set; }
    public DateTime CaughtAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? Notes { get; set; }
    public int UserId { get; set; }
    public int FishingSpotId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public FishingSpot FishingSpot { get; set; } = null!;
}
