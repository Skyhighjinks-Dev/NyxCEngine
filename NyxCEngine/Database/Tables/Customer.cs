using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NyxCEngine.Database.Tables
{
  public class Customer
  {
    public string CustomerId { get; set; } = null!;
    public string CustomerName { get; set; } = null!;

    public ICollection<Integration> Integrations { get; set; } = new List<Integration>();
    public CustomerSchedule? Schedule { get; set; }
    public CustomerBackground? Background { get; set; }
    public ICollection<BackgroundMedia> BackgroundMedia { get; set; } = new List<BackgroundMedia>();
    public ICollection<ScriptItem> Scripts { get; set; } = new List<ScriptItem>();
    public ICollection<VideoAsset> Assets { get; set; } = new List<VideoAsset>();
    public ICollection<ScheduledPost> ScheduledPosts { get; set; } = new List<ScheduledPost>();
  }

  public sealed class CustomerConfig : IEntityTypeConfiguration<Customer>
  {
    public void Configure(EntityTypeBuilder<Customer> b)
    {
      b.ToTable("customer", "dbo");

      b.HasKey(x => x.CustomerId);

      b.Property(x => x.CustomerId)
          .HasMaxLength(64)
          .IsRequired();

      b.Property(x => x.CustomerName)
          .HasMaxLength(256)
          .IsRequired();

      b.HasIndex(x => x.CustomerName);

      b.HasMany(c => c.Integrations)
       .WithOne(i => i.Customer)
       .HasForeignKey(i => i.CustomerId)
       .OnDelete(DeleteBehavior.Restrict);

      b.HasMany(c => c.ScheduledPosts)
       .WithOne(p => p.Customer)
       .HasForeignKey(p => p.CustomerId)
       .OnDelete(DeleteBehavior.Restrict);

      b.HasMany(c => c.Scripts)
       .WithOne(s => s.Customer)
       .HasForeignKey(s => s.CustomerId)
       .OnDelete(DeleteBehavior.Restrict);

      b.HasMany(c => c.Assets)
       .WithOne(v => v.Customer)
       .HasForeignKey(v => v.CustomerId)
       .OnDelete(DeleteBehavior.Restrict);

      b.HasOne(c => c.Schedule)
       .WithOne(s => s.Customer)
       .HasForeignKey<CustomerSchedule>(s => s.CustomerId)
       .OnDelete(DeleteBehavior.Cascade);

      b.HasOne(c => c.Background)
       .WithOne(bg => bg.Customer)
       .HasForeignKey<CustomerBackground>(bg => bg.CustomerId)
       .OnDelete(DeleteBehavior.Cascade);

      b.HasMany(c => c.BackgroundMedia)
       .WithOne(bg => bg.Customer)
       .HasForeignKey(bg => bg.CustomerId)
       .OnDelete(DeleteBehavior.SetNull);
    }
  }
}