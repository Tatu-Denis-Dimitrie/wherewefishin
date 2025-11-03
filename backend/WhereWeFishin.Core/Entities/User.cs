namespace WhereWeFishin.Core.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ProfilePictureUrl { get; set; }

    // Navigation properties
    public ICollection<FishingSpot> FishingSpots { get; set; } = new List<FishingSpot>();
    public ICollection<Catch> Catches { get; set; } = new List<Catch>();
}
