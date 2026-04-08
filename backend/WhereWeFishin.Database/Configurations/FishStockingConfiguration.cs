using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereWeFishin.Core.Entities;

namespace WhereWeFishin.Database.Configurations;

public class FishStockingConfiguration : IEntityTypeConfiguration<FishStocking>
{
    public void Configure(EntityTypeBuilder<FishStocking> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Species)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Notes)
            .HasMaxLength(500);

        builder.Property(s => s.StockingDate)
            .IsRequired();

        builder.Property(s => s.Quantity)
            .IsRequired();

        builder.HasOne(s => s.FishingSpot)
            .WithMany(f => f.FishStockings)
            .HasForeignKey(s => s.FishingSpotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.FishingSpotId);
    }
}
