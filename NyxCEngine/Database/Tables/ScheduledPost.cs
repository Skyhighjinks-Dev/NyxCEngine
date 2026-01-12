using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NyxCEngine.Database.Tables
{
  public class ScheduledPost
  {
    public int Id { get; set; }

    public string CustomerId { get; set; } = null!;
    public Customer Customer { get; set; } = null!;

    public string Platform { get; set; } = null!; // youtube/instagram/tiktok

    public string IntegrationId { get; set; } = null!;
    public Integration Integration { get; set; } = null!;

    public DateTime ScheduledAtUtc { get; set; }

    public string? PostizPostId { get; set; }
    public string? PostizState { get; set; }
    public string? ReleaseUrl { get; set; }

    public int? AssetId { get; set; }
    public VideoAsset? Asset { get; set; }

    public string Status { get; set; } = "scheduled";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
  }

  public sealed class ScheduledPostConfig : IEntityTypeConfiguration<ScheduledPost>
  {
    public void Configure(EntityTypeBuilder<ScheduledPost> b)
    {
      b.ToTable("scheduled_post", "dbo");

      b.HasKey(x => x.Id);

      b.Property(x => x.CustomerId).HasMaxLength(64).IsRequired();
      b.Property(x => x.Platform).HasMaxLength(32).IsRequired();

      b.Property(x => x.IntegrationId).HasMaxLength(128).IsRequired();
      b.Property(x => x.ScheduledAtUtc).HasColumnType("datetime2").IsRequired();

      b.Property(x => x.PostizPostId).HasMaxLength(128);
      b.Property(x => x.PostizState).HasMaxLength(64);
      b.Property(x => x.ReleaseUrl).HasMaxLength(2048);

      b.Property(x => x.Status).HasMaxLength(32).IsRequired().HasDefaultValue("scheduled");
      b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
      b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

      b.HasIndex(x => new { x.CustomerId, x.ScheduledAtUtc }).HasDatabaseName("idx_sched_customer_time");

      // UNIQUE (integration_id, scheduled_at_utc)
      b.HasIndex(x => new { x.IntegrationId, x.ScheduledAtUtc }).IsUnique();

      b.HasOne(p => p.Customer)
       .WithMany(c => c.ScheduledPosts)
       .HasForeignKey(p => p.CustomerId)
       .OnDelete(DeleteBehavior.Restrict);

      b.HasOne(p => p.Integration)
       .WithMany(i => i.ScheduledPosts)
       .HasForeignKey(p => p.IntegrationId)
       .OnDelete(DeleteBehavior.Restrict);

      b.HasOne(p => p.Asset)
       .WithMany(a => a.ScheduledPosts)
       .HasForeignKey(p => p.AssetId)
       .OnDelete(DeleteBehavior.SetNull);
    }
  }
}