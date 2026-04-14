using System.ComponentModel.DataAnnotations;

namespace WhereWeFishin.Core.DTOs;

public class PontoonDto
{
    public int Id { get; set; }
    public int FishingSpotId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double SouthWestLat { get; set; }
    public double SouthWestLng { get; set; }
    public double NorthEastLat { get; set; }
    public double NorthEastLng { get; set; }
    public string? Color { get; set; }
    public string? Coordinates { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePontoonDto
{
    [Range(1, int.MaxValue)]
    public int FishingSpotId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public double SouthWestLat { get; set; }
    public double SouthWestLng { get; set; }
    public double NorthEastLat { get; set; }
    public double NorthEastLng { get; set; }

    [MaxLength(20)]
    public string? Color { get; set; }
    public string? Coordinates { get; set; }
}

public class UpdatePontoonDto
{
    public string? Name { get; set; }
    public double? SouthWestLat { get; set; }
    public double? SouthWestLng { get; set; }
    public double? NorthEastLat { get; set; }
    public double? NorthEastLng { get; set; }
    public string? Color { get; set; }
    public string? Coordinates { get; set; }
}
