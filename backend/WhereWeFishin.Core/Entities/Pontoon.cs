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
    
    // JSON array of [lat, lng] coordinate pairs for polygon shape
    // e.g. "[[44.123,26.456],[44.124,26.457],[44.125,26.458]]"
    public string? Coordinates { get; set; }

    public FishingSpot FishingSpot { get; set; } = null!;
}
