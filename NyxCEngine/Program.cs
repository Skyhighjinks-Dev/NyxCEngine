using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NyxCEngine.APIs;
using NyxCEngine.APIs.ElevenLabs;
using NyxCEngine.APIs.Postiz;
using NyxCEngine.Database;
using NyxCEngine.Services;

namespace NyxCEngine
{
  internal class Program
  {
    private static string[] RequiredEnvVariables = new string[]
      {
        // Database
        "NYX_DB_CONNECTION",

        // Postiz
        "POSTIZ_API_KEY",
        "POSTIZ_BASE_PUBLIC_V1",

        // ElevenLabs (used by video pipeline)
        "ELEVENLABS_KEY"
      };

    internal static string ElevenLabsClientName = "ElevenLabsClient";
    internal static string PostizClientName = "PostizClient";


    static async Task Main(string[] args)
    {
      HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
      builder.Configuration.AddEnvironmentVariables();

      // 100% env-based config (no appsettings.json required)
      // Example:
      // NYX_DB_CONNECTION=Server=localhost;Database=NyxDatabase;User Id=NyxUser;Password=...;Encrypt=True;TrustServerCertificate=True;
      var cs = Environment.GetEnvironmentVariable("NYX_DB_CONNECTION")
               ?? builder.Configuration["NYX_DB_CONNECTION"]
               ?? "";

      VerifyRequiredEnvVars(RequiredEnvVariables);

      builder.Services.AddDbContextFactory<NyxDbContext>(opt =>
      {
        if (string.IsNullOrWhiteSpace(cs))
          throw new InvalidOperationException("NYX_DB_CONNECTION env var is required.");

        opt.UseSqlServer(cs, sql =>
        {
          sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        });
      });

      builder.Services.AddHttpClient(ElevenLabsClientName);
      builder.Services.AddHttpClient(PostizClientName, (sp, client) =>
      {
        // Load from env
        var baseUrl = Environment.GetEnvironmentVariable("POSTIZ_BASE_PUBLIC_V1") ?? "";
        if (string.IsNullOrWhiteSpace(baseUrl))
          throw new InvalidOperationException("POSTIZ_BASE_PUBLIC_V1 env var is required.");

        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromMinutes(10); // uploads can be big
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        // API key header (Postiz expects Authorization: <key>)
        var apiKey = Environment.GetEnvironmentVariable("POSTIZ_API_KEY") ?? "";
        if (string.IsNullOrWhiteSpace(apiKey))
          throw new InvalidOperationException("POSTIZ_API_KEY env var is required.");

        client.DefaultRequestHeaders.Remove("Authorization");
        client.DefaultRequestHeaders.Add("Authorization", apiKey);
      })
      .AddPolicyHandler(PostizPollyPolicies.RetryWithJitter())
      .AddPolicyHandler(PostizPollyPolicies.TimeoutPolicy());


      builder.Services.AddSingleton<ElevenLabsEngine>();
      builder.Services.AddSingleton<PostizEngine>();

      // Should start these background services automatically
      builder.Services.AddHostedService<VideoPipelineWorker>();
      builder.Services.AddHostedService<VideoReconcilerWorker>();

      var app = builder.Build();
      await ApplyMigrationsWithRetryAsync(app.Services);
      await app.RunAsync();
    }


    static void VerifyRequiredEnvVars(params string[] keys)
    {
      var missing = keys.Where(k => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(k))).ToList();
      if (missing.Count > 0)
        throw new InvalidOperationException("Missing required env vars: " + string.Join(", ", missing));
    }


    static async Task ApplyMigrationsWithRetryAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
      const int maxAttempts = 10;
      TimeSpan delay = TimeSpan.FromSeconds(2);

      for (var x = 0; x < maxAttempts; x++)
      {
        try
        {
          using var scope = serviceProvider.CreateScope();
          var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NyxDbContext>>();
          await using var db = await dbFactory.CreateDbContextAsync(ct);
          await db.Database.MigrateAsync(ct);
          return;
        }
        catch (Exception ex) when (x < maxAttempts - 1)
        {
          Console.WriteLine($"Migration attempt {x + 1} failed: {ex.Message}. Retrying in {delay.TotalSeconds} seconds...");
          await Task.Delay(delay, ct);
          delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30)); // Max 30 second delay
        }
      }
    }
  }
}