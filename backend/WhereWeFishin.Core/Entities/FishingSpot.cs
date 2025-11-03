namespace WhereWeFishin.Core.Entities;

public class FishingSpot : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ImageUrl { get; set; }
    public Guid UserId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<Catch> Catches { get; set; } = new List<Catch>();
}
