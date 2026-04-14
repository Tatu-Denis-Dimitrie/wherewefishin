using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.Database.Configurations;

public class VideoAnalysisConfiguration : IEntityTypeConfiguration<VideoAnalysis>
{
    public void Configure(EntityTypeBuilder<VideoAnalysis> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(v => v.VideoUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(v => v.ProcessedVideoUrl)
            .HasMaxLength(500);

        builder.Property(v => v.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<AnalysisStatus>(v));

        builder.Property(v => v.DominantFishType)
            .HasMaxLength(100);

        builder.Property(v => v.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => v.UserId);
        builder.HasIndex(v => v.Status);
        builder.HasIndex(v => v.AnalyzedAt);
    }
}
