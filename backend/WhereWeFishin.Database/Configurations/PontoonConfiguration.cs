using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereWeFishin.Core.Entities;

namespace WhereWeFishin.Database.Configurations;

public class PontoonConfiguration : IEntityTypeConfiguration<Pontoon>
{
    public void Configure(EntityTypeBuilder<Pontoon> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Color)
            .HasMaxLength(20);

        builder.Property(p => p.SouthWestLat)
            .HasPrecision(18, 15);

        builder.Property(p => p.SouthWestLng)
            .HasPrecision(18, 15);

        builder.Property(p => p.NorthEastLat)
            .HasPrecision(18, 15);

        builder.Property(p => p.NorthEastLng)
            .HasPrecision(18, 15);

        builder.HasOne(p => p.FishingSpot)
            .WithMany(f => f.Pontoons)
            .HasForeignKey(p => p.FishingSpotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.FishingSpotId);
    }
}
