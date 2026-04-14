using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.Database.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(UserRole.User)
            .HasConversion<string>();

        builder.Property(u => u.FirstName)
            .HasMaxLength(50);

        builder.Property(u => u.LastName)
            .HasMaxLength(50);

        builder.HasIndex(u => u.Username)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasMany(u => u.FishingSpots)
            .WithOne(f => f.User)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
