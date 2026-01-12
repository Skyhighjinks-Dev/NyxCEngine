using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace NyxCEngine.Database.Tables
{
  public class ScriptItem
  {
    public int Id { get; set; }

    public string CustomerId { get; set; } = null!;
    public Customer Customer { get; set; } = null!;

    public string FilePath { get; set; } = null!;
    public string ContentSha1 { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UsedAtUtc { get; set; }
    public DateTime? ReservedUntilUtc { get; set; }
  }

  public sealed class ScriptItemConfig : IEntityTypeConfiguration<ScriptItem>
  {
    public void Configure(EntityTypeBuilder<ScriptItem> b)
    {
      b.ToTable("script_item", "dbo");

      b.HasKey(x => x.Id);

      b.Property(x => x.CustomerId).HasMaxLength(64).IsRequired();
      b.Property(x => x.FilePath).HasMaxLength(2048).IsRequired();
      b.Property(x => x.ContentSha1).HasMaxLength(40).IsRequired(); // sha1 hex

      b.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").IsRequired();
      b.Property(x => x.UsedAtUtc).HasColumnType("datetime2");
      b.Property(x => x.ReservedUntilUtc).HasColumnType("datetime2");

      b.HasIndex(x => x.FilePath).IsUnique(); // matches UNIQUE(file_path)
      b.HasIndex(x => new { x.CustomerId, x.UsedAtUtc }).HasDatabaseName("idx_script_customer_unused");

      b.HasOne(s => s.Customer)
       .WithMany(c => c.Scripts)
       .HasForeignKey(s => s.CustomerId)
       .OnDelete(DeleteBehavior.Restrict);
    }
  }
}
