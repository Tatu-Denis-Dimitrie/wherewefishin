using System.ComponentModel.DataAnnotations;

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
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [Range(0, 100_000)]
    public decimal PricePerHour { get; set; } = 0;

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
    public bool ClearManager { get; set; }
    public int? DefaultZoom { get; set; }
    public double? DefaultCenterLat { get; set; }
    public double? DefaultCenterLng { get; set; }
    public bool ResetDefaultMapView { get; set; }
    public string? FishSpecies { get; set; }
}

public class SpotStatisticsDto
{
    public int TotalBookings { get; set; }
    public int ActiveBookings { get; set; }
    public int CancelledBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalReviews { get; set; }
    public double? AverageRating { get; set; }
    public int TotalPontoons { get; set; }
    public int TotalEmployees { get; set; }
    public int TotalStockings { get; set; }
}
