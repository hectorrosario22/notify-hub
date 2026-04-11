using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;

namespace NotifyHub.Infrastructure.Persistence.EntityConfigurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnType("uuid");

        builder.Property(n => n.RecipientUserId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(n => n.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(n => n.Body)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(n => n.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(n => n.IsRead).IsRequired();

        builder.Property(n => n.ReadAt);

        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.UpdatedAt).IsRequired();

        builder.Navigation(n => n.Deliveries)
            .HasField("_deliveries")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(n => n.Deliveries)
            .WithOne()
            .HasForeignKey(d => d.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => n.RecipientUserId);
        builder.HasIndex(n => new { n.RecipientUserId, n.IsRead });
    }
}
