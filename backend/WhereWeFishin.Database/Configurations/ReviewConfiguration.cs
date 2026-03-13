using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereWeFishin.Core.Entities;

namespace WhereWeFishin.Database.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Rating)
            .IsRequired();

        builder.Property(r => r.Comment)
            .HasMaxLength(2000);

        builder.HasOne(r => r.FishingSpot)
            .WithMany(f => f.Reviews)
            .HasForeignKey(r => r.FishingSpotId)
            .OnDelete(DeleteBehavior.Cascade);

        // Use NoAction to avoid multiple cascade paths with User
        builder.HasOne(r => r.User)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(r => r.FishingSpotId);
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => new { r.UserId, r.FishingSpotId }).IsUnique();
    }
}
