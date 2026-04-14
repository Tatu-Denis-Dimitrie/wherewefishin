using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereWeFishin.Core.Entities;

namespace WhereWeFishin.Database.Configurations;

public class SpotEmployeeConfiguration : IEntityTypeConfiguration<SpotEmployee>
{
    public void Configure(EntityTypeBuilder<SpotEmployee> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.FishingSpot)
            .WithMany(s => s.SpotEmployees)
            .HasForeignKey(e => e.FishingSpotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.FishingSpotId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(e => e.FishingSpotId);
    }
}
