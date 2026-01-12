using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NyxCEngine.Database.Tables
{
  public class IntegrationPolicy
  {
    public string Platform { get; set; } = null!;
    public string PostType { get; set; } = null!; // post/story/etc
    public bool IsEnabled { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
  }

  public sealed class IntegrationPolicyConfig : IEntityTypeConfiguration<IntegrationPolicy>
  {
    public void Configure(EntityTypeBuilder<IntegrationPolicy> b)
    {
      b.ToTable("integration_policy", "dbo");

      // PRIMARY KEY (platform, post_type)
      b.HasKey(x => new { x.Platform, x.PostType });

      b.Property(x => x.Platform).HasMaxLength(32).IsRequired();
      b.Property(x => x.PostType).HasMaxLength(32).IsRequired();

      b.Property(x => x.IsEnabled).HasColumnType("bit").IsRequired();
      b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
    }
  }
}
