using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NyxCEngine.Database;

namespace NyxCEngine.Services
{
  internal class VideoReconcilerWorker : BackgroundService
  {
    private readonly IDbContextFactory<NyxDbContext> _dbFactory;
    private readonly IConfiguration _configuration;

    public VideoReconcilerWorker(IDbContextFactory<NyxDbContext> dbFactory, IConfiguration configuration)
    {
      _dbFactory = dbFactory;
      _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      do
      {
        Console.WriteLine($"[{DateTime.Now.ToString("g")}] {nameof(VideoReconcilerWorker)} has ran an execution cycle!");
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
      } while (!stoppingToken.IsCancellationRequested);
    }
  }
}
