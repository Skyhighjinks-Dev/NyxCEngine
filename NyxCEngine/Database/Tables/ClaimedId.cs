using Microsoft.EntityFrameworkCore;

namespace NyxCEngine.Database.Tables
{
  public sealed class ClaimedId
  {
    public Guid Id { get; set; }
  }

  public sealed class ClaimedIdConfig : IEntityTypeConfiguration<ClaimedId>
  {
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ClaimedId> b)
    {
      b.ToTable("claimed_id", "dbo");
      b.HasNoKey();
    }
  }
}
