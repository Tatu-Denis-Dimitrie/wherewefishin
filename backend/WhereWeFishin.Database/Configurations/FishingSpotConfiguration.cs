using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereWeFishin.Core.Entities;

namespace WhereWeFishin.Database.Configurations;

public class FishingSpotConfiguration : IEntityTypeConfiguration<FishingSpot>
{
    public void Configure(EntityTypeBuilder<FishingSpot> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.Description)
            .HasMaxLength(500);

        builder.Property(f => f.Latitude)
            .IsRequired()
            .HasPrecision(9, 6);

        builder.Property(f => f.Longitude)
            .IsRequired()
            .HasPrecision(9, 6);

        builder.HasOne(f => f.User)
            .WithMany(u => u.FishingSpots)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Manager)
            .WithMany()
            .HasForeignKey(f => f.ManagerId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.Property(f => f.PricePerHour)
            .HasPrecision(10, 2)
            .HasDefaultValue(0m);

        builder.HasMany(f => f.Catches)
            .WithOne(c => c.FishingSpot)
            .HasForeignKey(c => c.FishingSpotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
