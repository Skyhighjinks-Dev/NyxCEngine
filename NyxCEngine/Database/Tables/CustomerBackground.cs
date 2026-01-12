using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace NyxCEngine.Database.Tables
{
  public class CustomerBackground
  {
    public string CustomerId { get; set; } = null!;
    public Customer Customer { get; set; } = null!;

    public string Strategy { get; set; } = "round_robin"; // round_robin|random|fixed
    public string? FixedFilePath { get; set; }
    public int Cursor { get; set; } = 0;

    public DateTime UpdatedAtUtc { get; set; }
  }

  public sealed class CustomerBackgroundConfig : IEntityTypeConfiguration<CustomerBackground>
  {
    public void Configure(EntityTypeBuilder<CustomerBackground> b)
    {
      b.ToTable("customer_background", "dbo");

      b.HasKey(x => x.CustomerId);

      b.Property(x => x.CustomerId).HasMaxLength(64).IsRequired();
      b.Property(x => x.Strategy).HasMaxLength(32).IsRequired().HasDefaultValue("round_robin");
      b.Property(x => x.FixedFilePath).HasMaxLength(2048);

      b.Property(x => x.Cursor).HasDefaultValue(0);
      b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();

      b.HasOne(bg => bg.Customer)
       .WithOne(c => c.Background)
       .HasForeignKey<CustomerBackground>(bg => bg.CustomerId)
       .OnDelete(DeleteBehavior.Cascade);
    }
  }
}
