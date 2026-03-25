using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereWeFishin.Core.Entities;

namespace WhereWeFishin.Database.Configurations;

public class FishingSessionConfiguration : IEntityTypeConfiguration<FishingSession>
{
    public void Configure(EntityTypeBuilder<FishingSession> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TotalPrice)
            .HasPrecision(10, 2);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.VerificationToken)
            .HasMaxLength(64);

        builder.Property(s => s.StartDate)
            .IsRequired();

        builder.Property(s => s.DurationHours)
            .IsRequired();

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.FishingSpot)
            .WithMany(f => f.Sessions)
            .HasForeignKey(s => s.FishingSpotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.FishingSpotId);
        builder.HasIndex(s => s.Status);
    }
}
