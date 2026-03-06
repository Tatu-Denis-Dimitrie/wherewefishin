using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereWeFishin.Core.Entities;

namespace WhereWeFishin.Database.Configurations;

public class CatchConfiguration : IEntityTypeConfiguration<Catch>
{
    public void Configure(EntityTypeBuilder<Catch> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FishSpecies)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Weight)
            .HasPrecision(8, 2);

        builder.Property(c => c.Length)
            .HasPrecision(8, 2);

        builder.Property(c => c.Notes)
            .HasMaxLength(1000);

        builder.HasOne(c => c.User)
            .WithMany(u => u.Catches)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.FishingSpot)
            .WithMany(f => f.Catches)
            .HasForeignKey(c => c.FishingSpotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
