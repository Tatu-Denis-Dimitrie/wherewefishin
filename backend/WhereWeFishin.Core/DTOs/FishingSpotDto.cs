namespace WhereWeFishin.Core.DTOs;

public class FishingSpotDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ImageUrl { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFishingSpotDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ImageUrl { get; set; }
}

public class UpdateFishingSpotDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ImageUrl { get; set; }
}
