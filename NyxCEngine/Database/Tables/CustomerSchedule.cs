using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NyxCEngine.Database.Tables
{
  public class CustomerSchedule
  {
    public string CustomerId { get; set; } = null!;
    public Customer Customer { get; set; } = null!;

    public string Timezone { get; set; } = "Europe/London";
    public string PostingTimes { get; set; } = "09:00,14:00,20:00";
    public int BufferDays { get; set; } = 7;
    public int JitterMinutes { get; set; } = 0;

    public DateTime UpdatedAtUtc { get; set; }
  }

  public sealed class CustomerScheduleConfig : IEntityTypeConfiguration<CustomerSchedule>
  {
    public void Configure(EntityTypeBuilder<CustomerSchedule> b)
    {
      b.ToTable("customer_schedule", "dbo");

      b.HasKey(x => x.CustomerId);

      b.Property(x => x.CustomerId).HasMaxLength(64).IsRequired();
      b.Property(x => x.Timezone).HasMaxLength(64).IsRequired().HasDefaultValue("Europe/London");
      b.Property(x => x.PostingTimes).HasMaxLength(256).IsRequired().HasDefaultValue("09:00,14:00,20:00");

      b.Property(x => x.BufferDays).HasDefaultValue(7);
      b.Property(x => x.JitterMinutes).HasDefaultValue(0);

      b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();

      b.HasIndex(x => x.CustomerId);
      b.HasOne(s => s.Customer)
       .WithOne(c => c.Schedule)
       .HasForeignKey<CustomerSchedule>(s => s.CustomerId)
       .OnDelete(DeleteBehavior.Cascade);
    }
  }
}