namespace WhereWeFishin.Core.Entities;

public class FishStocking : BaseEntity
{
    public int FishingSpotId { get; set; }
    public DateTime StockingDate { get; set; }
    public string Species { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Notes { get; set; }

    public FishingSpot FishingSpot { get; set; } = null!;
}
