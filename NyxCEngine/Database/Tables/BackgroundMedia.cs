using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace NyxCEngine.Database.Tables
{
  public class BackgroundMedia
  {
    public string FilePath { get; set; } = null!; // PK

    public string? CustomerId { get; set; } // NULL = global pool
    public Customer? Customer { get; set; }

    public double? DurationSeconds { get; set; }
    public double EndBufferSeconds { get; set; } = 10;
    public bool Enabled { get; set; } = true;

    public DateTime LastScannedAtUtc { get; set; }
  }

  public sealed class BackgroundMediaConfig : IEntityTypeConfiguration<BackgroundMedia>
  {
    public void Configure(EntityTypeBuilder<BackgroundMedia> b)
    {
      b.ToTable("background_media", "dbo");

      b.HasKey(x => x.FilePath);

      b.Property(x => x.FilePath).HasMaxLength(2048).IsRequired();
      b.Property(x => x.CustomerId).HasMaxLength(64);

      b.Property(x => x.DurationSeconds);
      b.Property(x => x.EndBufferSeconds).HasDefaultValue(10);
      b.Property(x => x.Enabled).HasColumnType("bit").HasDefaultValue(true);

      b.Property(x => x.LastScannedAtUtc).HasColumnType("datetime2").IsRequired();

      b.HasIndex(x => x.CustomerId).HasDatabaseName("idx_bg_customer");

      b.HasOne(bg => bg.Customer)
       .WithMany(c => c.BackgroundMedia)
       .HasForeignKey(bg => bg.CustomerId)
       .OnDelete(DeleteBehavior.SetNull);
    }
  }
}
