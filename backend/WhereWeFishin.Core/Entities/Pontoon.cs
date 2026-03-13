namespace WhereWeFishin.Core.Entities;

public class Pontoon : BaseEntity
{
    public int FishingSpotId { get; set; }
    public string Name { get; set; } = string.Empty;
    
    // Coordinates for the rectangle bounds (southwest and northeast corners)
    public double SouthWestLat { get; set; }
    public double SouthWestLng { get; set; }
    public double NorthEastLat { get; set; }
    public double NorthEastLng { get; set; }
    
    public string? Color { get; set; } // Optional color for display

    public FishingSpot FishingSpot { get; set; } = null!;
}
