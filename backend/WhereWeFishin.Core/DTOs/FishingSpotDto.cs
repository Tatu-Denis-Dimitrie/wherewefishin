namespace WhereWeFishin.Core.DTOs;

public class FishingSpotDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ImageUrl { get; set; }
    public decimal PricePerHour { get; set; }
    public int UserId { get; set; }
    public int? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public int? DefaultZoom { get; set; }
    public double? DefaultCenterLat { get; set; }
    public double? DefaultCenterLng { get; set; }
    public string? FishSpecies { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFishingSpotDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ImageUrl { get; set; }
    public decimal PricePerHour { get; set; } = 0;
    public int UserId { get; set; }
    public int? ManagerId { get; set; }
}

public class UpdateFishingSpotDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? PricePerHour { get; set; }
    public int? ManagerId { get; set; }
    public int? DefaultZoom { get; set; }
    public double? DefaultCenterLat { get; set; }
    public double? DefaultCenterLng { get; set; }
    public bool ResetDefaultMapView { get; set; }
    public string? FishSpecies { get; set; }
}
