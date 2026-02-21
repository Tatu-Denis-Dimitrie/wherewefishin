namespace WhereWeFishin.Core.Entities;

public class FishingSpot : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ImageUrl { get; set; }
    public decimal PricePerHour { get; set; } = 0;
    public int UserId { get; set; }
    public int? ManagerId { get; set; }

    public User User { get; set; } = null!;
    public User? Manager { get; set; }
    public ICollection<Catch> Catches { get; set; } = new List<Catch>();
    public ICollection<FishingSession> Sessions { get; set; } = new List<FishingSession>();
}
