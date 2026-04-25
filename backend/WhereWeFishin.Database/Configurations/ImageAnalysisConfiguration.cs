using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereWeFishin.Core.Entities;

namespace WhereWeFishin.Database.Configurations;

public class ImageAnalysisConfiguration : IEntityTypeConfiguration<ImageAnalysis>
{
    public void Configure(EntityTypeBuilder<ImageAnalysis> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(i => i.ProcessedImageUrl)
            .HasMaxLength(500);

        builder.Property(i => i.DominantFishType)
            .HasMaxLength(100);

        builder.HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.UserId);
        builder.HasIndex(i => i.AnalyzedAt);
    }
}
