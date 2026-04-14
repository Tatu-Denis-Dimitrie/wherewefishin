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
