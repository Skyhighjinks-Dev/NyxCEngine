using Microsoft.EntityFrameworkCore;
using NyxCEngine.Database.Tables;
using System.Reflection;

namespace NyxCEngine.Database
{
  internal class NyxDbContext : DbContext
  {
    public DbSet<BackgroundMedia> BackgroundMedias { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<CustomerBackground> CustomerBackgrounds { get; set; } = null!;
    public DbSet<CustomerSchedule> CustomerSchedules { get; set; } = null!;
    public DbSet<Integration> Integrations { get; set; } = null!;
    public DbSet<IntegrationOverride> IntegrationOverrides { get; set; } = null!;
    public DbSet<IntegrationPolicy> IntegrationPolicies { get; set; } = null!;
    public DbSet<ScheduledPost> ScheduledPosts { get; set; } = null!;
    public DbSet<ScriptItem> ScriptItems { get; set; } = null!;
    public DbSet<VideoAsset> VideoAssets { get; set; } = null!;
    public DbSet<PremadeVideoSeries> PremadeVideoSeries { get; set; } = null!;
    public DbSet<ClaimedId> ClaimedIds => null!;

    public NyxDbContext(DbContextOptions<NyxDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      modelBuilder.HasDefaultSchema("dbo");
      modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
      base.OnModelCreating(modelBuilder);
    }
  }
}