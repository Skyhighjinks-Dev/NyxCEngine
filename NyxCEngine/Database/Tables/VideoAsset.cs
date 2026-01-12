using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NyxCEngine.Database.Tables
{
  public class VideoAsset
  {
    public enum VideoAssetSourceType
    {
      Generated = 0,
      PremadeSegment = 1
    }

    public int Id { get; set; }

    public string CustomerId { get; set; } = null!;
    public Customer Customer { get; set; } = null!;

    public string? Title { get; set; }
    public string? ScriptFilePath { get; set; }
    public string? ScriptSha1 { get; set; }

    public string? WavPath { get; set; }
    public string? TimestampsPath { get; set; }
    public double? AudioDurationSeconds { get; set; }

    public string? BackgroundFilePath { get; set; }
    public double? BackgroundStartOffsetSeconds { get; set; }
    public double? EndBufferSecondsUsed { get; set; }

    public string Mp4Path { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ScheduledPost> ScheduledPosts { get; set; } = new List<ScheduledPost>();

    public Guid? SeriesId { get; set; }
    public int? SeriesIndex { get; set; }
    public int? SeriesCount { get; set; }
    public string? TargetIntegrationId { get; set; }

    public VideoAssetSourceType SourceType { get; set; } // enum
    public string? ThumbnailPath { get; set; }
  }

  public sealed class VideoAssetConfig : IEntityTypeConfiguration<VideoAsset>
  {
    public void Configure(EntityTypeBuilder<VideoAsset> b)
    {
      b.ToTable("video_asset", "dbo");

      b.HasKey(x => x.Id);

      b.Property(x => x.CustomerId).HasMaxLength(64).IsRequired();
      b.Property(x => x.Title).HasMaxLength(256);

      b.Property(x => x.ScriptFilePath).HasMaxLength(2048);
      b.Property(x => x.ScriptSha1).HasMaxLength(40);

      b.Property(x => x.WavPath).HasMaxLength(2048);
      b.Property(x => x.TimestampsPath).HasMaxLength(2048);

      b.Property(x => x.BackgroundFilePath).HasMaxLength(2048);
      b.Property(x => x.Mp4Path).HasMaxLength(2048).IsRequired();

      b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").IsRequired();

      b.Property(x => x.SeriesId)
        .HasColumnType("uniqueidentifier"); 

      b.Property(x => x.SeriesIndex);
      b.Property(x => x.SeriesCount);

      b.Property(x => x.TargetIntegrationId)
        .HasMaxLength(128);

      b.Property(x => x.SourceType)
        .IsRequired()
        .HasConversion<int>();

      b.Property(x => x.ThumbnailPath).HasMaxLength(2048);

      b.HasOne(v => v.Customer)
       .WithMany(c => c.Assets)
       .HasForeignKey(v => v.CustomerId)
       .OnDelete(DeleteBehavior.Restrict);

      b.HasIndex(x => new { x.SeriesId, x.SeriesIndex });

      b.HasIndex(x => new { x.CustomerId, x.SourceType });

      b.HasIndex(x => x.TargetIntegrationId);
    }
  }
}
