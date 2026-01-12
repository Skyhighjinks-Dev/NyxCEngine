using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NyxCEngine.Database.Tables
{
  public sealed class PremadeVideoSeries
  {
    public enum PremadeSeriesStatus
    {
      PendingSplit = 0,
      SplitComplete = 1,
      Failed = 2
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerId { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public string SourcePath { get; set; } = null!;
    public int SegmentSeconds { get; set; }
    public string? TargetIntegrationId { get; set; }
    public PremadeSeriesStatus Status { get; set; } = PremadeSeriesStatus.PendingSplit;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SplitAtUtc { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public string? LockOwner { get; set; }
  }

  public sealed class PremadeVideoSeriesConfig : IEntityTypeConfiguration<PremadeVideoSeries>
  {
    public void Configure(EntityTypeBuilder<PremadeVideoSeries> b)
    {
      b.ToTable("premade_video_series", "dbo");

      b.HasKey(x => x.Id);

      b.Property(x => x.Id)
          .HasColumnType("uniqueidentifier")
          .ValueGeneratedNever();

      b.Property(x => x.CustomerId)
          .HasMaxLength(64)
          .IsRequired();

      b.Property(x => x.SourcePath)
          .HasMaxLength(2048)
          .IsRequired();

      b.Property(x => x.SegmentSeconds)
          .IsRequired();

      b.Property(x => x.Status)
          .HasConversion<int>()
          .IsRequired();

      b.Property(x => x.CreatedAtUtc)
          .HasColumnType("datetime2")
          .IsRequired();

      b.Property(x => x.SplitAtUtc)
          .HasColumnType("datetime2");

      b.Property(x => x.LockedAtUtc)
          .HasColumnType("datetime2");

      b.Property(x => x.LockOwner)
          .HasMaxLength(128);

      b.Property(x => x.TargetIntegrationId)
          .HasMaxLength(128);

      b.HasOne(x => x.Customer)
          .WithMany()
          .HasForeignKey(x => x.CustomerId)
          .OnDelete(DeleteBehavior.Restrict);

      b.HasIndex(x => new { x.Status, x.LockedAtUtc });
      b.HasIndex(x => x.CustomerId);
    }
  }
}