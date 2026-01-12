using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NyxCEngine.Database.Tables
{
  public class Integration
  {
    public string IntegrationId { get; set; } = null!;
    public string Identifier { get; set; } = null!; // youtube/instagram/tiktok

    public string? Profile { get; set; }
    public string? Name { get; set; }
    public bool Disabled { get; set; }

    public string CustomerId { get; set; } = null!;
    public Customer Customer { get; set; } = null!;

    public string? Picture { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public IntegrationOverride? Override { get; set; }
    public ICollection<ScheduledPost> ScheduledPosts { get; set; } = new List<ScheduledPost>();
  }

  public sealed class IntegrationConfig : IEntityTypeConfiguration<Integration>
  {
    public void Configure(EntityTypeBuilder<Integration> b)
    {
      b.ToTable("integration", "dbo");

      b.HasKey(x => x.IntegrationId);

      b.Property(x => x.IntegrationId).HasMaxLength(128).IsRequired();
      b.Property(x => x.Identifier).HasMaxLength(32).IsRequired();

      b.Property(x => x.Profile).HasMaxLength(256);
      b.Property(x => x.Name).HasMaxLength(256);
      b.Property(x => x.Picture).HasMaxLength(1024);

      b.Property(x => x.Disabled).HasColumnType("bit").HasDefaultValue(false);

      b.Property(x => x.CustomerId).HasMaxLength(64).IsRequired();
      b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2").IsRequired();

      b.HasIndex(x => x.CustomerId).HasDatabaseName("idx_integration_customer");

      b.HasOne(i => i.Customer)
       .WithMany(c => c.Integrations)
       .HasForeignKey(i => i.CustomerId)
       .OnDelete(DeleteBehavior.Restrict);

      b.HasMany(i => i.ScheduledPosts)
       .WithOne(p => p.Integration)
       .HasForeignKey(p => p.IntegrationId)
       .OnDelete(DeleteBehavior.Restrict);

      b.HasOne(i => i.Override)
       .WithOne(o => o.Integration)
       .HasForeignKey<IntegrationOverride>(o => o.IntegrationId)
       .OnDelete(DeleteBehavior.Cascade);
    }
  }
}