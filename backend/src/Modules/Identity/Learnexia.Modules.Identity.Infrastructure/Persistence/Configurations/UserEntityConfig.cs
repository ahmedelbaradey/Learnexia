using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Identity.Infrastructure.Persistence.Configurations;

public class UserEntityConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasOne(u => u.CreatedByUser)
            .WithMany()
            .HasForeignKey(u => u.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.UpdatedByUser)
            .WithMany()
            .HasForeignKey(u => u.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.DeletedByUser)
            .WithMany()
            .HasForeignKey(u => u.DeletedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(255)
            .HasComment("User's full name for display purposes");

        builder.Property(u => u.PreferredLanguage)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("ar-EG")
            .HasComment("User's preferred language (ar-EG or en-US)");

        builder.Property(u => u.CountryCode)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("+20")
            .HasComment("Country code for mobile number");

        builder.Property(u => u.Nationality).HasMaxLength(100).HasComment("User's nationality");
        builder.Property(u => u.PersonalPhotoPath).HasMaxLength(500).HasComment("File path for user's personal photo");

        builder.Property(u => u.RegistrationMessageIsSent)
            .IsRequired().HasDefaultValue(false)
            .HasComment("Flag indicating if registration message has been sent");

        builder.Property(u => u.RegistrationIsCompleted)
            .IsRequired().HasDefaultValue(false)
            .HasComment("Flag indicating if user has completed registration");

        builder.Property(u => u.IsActive)
            .IsRequired().HasDefaultValue(false)
            .HasComment("User status (active/inactive)");

        builder.Property(u => u.CreatedAt)
            .IsRequired().HasDefaultValueSql("now()")
            .HasComment("Timestamp when the user was created");

        builder.Property(u => u.UpdatedAt)
            .HasDefaultValueSql("now()")
            .HasComment("Timestamp when the user was last updated");

        builder.Property(u => u.IsDeleted).HasDefaultValue(false).HasComment("Soft delete flag");

        builder.HasIndex(u => u.FullName).HasDatabaseName("IX_Users_FullName_Performance");
        builder.HasIndex(u => u.PhoneNumber)
            .HasDatabaseName("IX_Users_PhoneNumber_Performance")
            .IsUnique()
            .HasFilter("\"PhoneNumber\" IS NOT NULL");
        builder.HasIndex(u => u.IsActive).HasDatabaseName("IX_Users_IsActive_Performance");
    }
}
