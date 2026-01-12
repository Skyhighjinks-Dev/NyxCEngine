using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace NyxCEngine.Database
{
  internal class NyxDbContextFactory : IDesignTimeDbContextFactory<NyxDbContext>
  {
    public NyxDbContext CreateDbContext(string[] args)
    {
      var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddEnvironmentVariables()
        .Build();

      var connectionString = configuration["NYX_DB_CONNECTION"];

      var optionsBuilder = new DbContextOptionsBuilder<NyxDbContext>();
      optionsBuilder.UseSqlServer(connectionString);
      return new NyxDbContext(optionsBuilder.Options);
    }
  }
}
