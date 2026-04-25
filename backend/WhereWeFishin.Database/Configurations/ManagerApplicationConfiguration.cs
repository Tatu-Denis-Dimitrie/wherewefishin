using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.Database.Configurations;

public class ManagerApplicationConfiguration : IEntityTypeConfiguration<ManagerApplication>
{
    public void Configure(EntityTypeBuilder<ManagerApplication> builder)
    {
        builder.HasKey(application => application.Id);

        builder.Property(application => application.LakeName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(application => application.Description)
            .HasMaxLength(500);

        builder.Property(application => application.Latitude)
            .IsRequired()
            .HasPrecision(9, 6);

        builder.Property(application => application.Longitude)
            .IsRequired()
            .HasPrecision(9, 6);

        builder.Property(application => application.LocationLabel)
            .HasMaxLength(250);

        builder.Property(application => application.ProposedPricePerHour)
            .HasPrecision(10, 2)
            .HasDefaultValue(0m);

        builder.Property(application => application.ContactPhone)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(application => application.Motivation)
            .IsRequired()
            .HasMaxLength(1500);

        builder.Property(application => application.AdministrationBasis)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(application => application.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ManagerApplicationStatus.Pending);

        builder.Property(application => application.RejectionReason)
            .HasMaxLength(1000);

        builder.HasOne(application => application.ApplicantUser)
            .WithMany()
            .HasForeignKey(application => application.ApplicantUserId)
                .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(application => application.ReviewedByAdmin)
            .WithMany()
            .HasForeignKey(application => application.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(application => application.ApprovedFishingSpot)
            .WithMany()
            .HasForeignKey(application => application.ApprovedFishingSpotId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(application => application.ApplicantUserId);
        builder.HasIndex(application => application.Status);
        builder.HasIndex(application => new { application.ApplicantUserId, application.Status })
            .IsUnique()
            .HasFilter("[Status] = 'Pending' AND [IsDeleted] = 0");
    }
}