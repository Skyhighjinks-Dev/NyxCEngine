using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NyxCEngine.Database.Tables
{
  public class IntegrationOverride
  {
    public string IntegrationId { get; set; } = null!;
    public Integration? Integration { get; set; }

    public bool? ManualDisabled { get; set; }
    public string? Note { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
  }

  public sealed class IntegrationOverrideConfig : IEntityTypeConfiguration<IntegrationOverride>
  {
    public void Configure(EntityTypeBuilder<IntegrationOverride> b)
    {
      b.ToTable("integration_override", "dbo");

      b.HasKey(x => x.IntegrationId);

      b.Property(x => x.IntegrationId).HasMaxLength(128).IsRequired();
      b.Property(x => x.ManualDisabled).HasColumnType("bit");
      b.Property(x => x.Note).HasMaxLength(1024);
      b.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

      b.HasOne(o => o.Integration)
       .WithOne(i => i.Override)
       .HasForeignKey<IntegrationOverride>(o => o.IntegrationId)
       .OnDelete(DeleteBehavior.Cascade);
    }
  }
}
