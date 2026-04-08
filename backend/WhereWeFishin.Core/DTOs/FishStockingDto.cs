namespace WhereWeFishin.Core.DTOs;

public class FishStockingDto
{
    public int Id { get; set; }
    public int FishingSpotId { get; set; }
    public DateTime StockingDate { get; set; }
    public string Species { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFishStockingDto
{
    public DateTime StockingDate { get; set; }
    public string Species { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}

public class UpdateFishStockingDto
{
    public DateTime? StockingDate { get; set; }
    public string? Species { get; set; }
    public int? Quantity { get; set; }
    public string? Notes { get; set; }
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
