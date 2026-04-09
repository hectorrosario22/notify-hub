using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotifyHub.Core.Entities;

namespace NotifyHub.Infrastructure.Persistence.EntityConfigurations;

internal sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_deliveries");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnType("uuid");

        builder.Property(d => d.NotificationId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(d => d.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.Recipient)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.RetryCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(d => d.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(d => d.SentAt);

        builder.Property(d => d.CreatedAt).IsRequired();

        builder.HasIndex(d => d.NotificationId);
    }
}
