using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NyxCEngine.APIs;
using NyxCEngine.APIs.ElevenLabs;
using NyxCEngine.APIs.Postiz;
using NyxCEngine.Database;
using NyxCEngine.Services;
using NyxCEngine.Util;

namespace NyxCEngine
{
  internal class Program
  {
    private static string[] RequiredEnvVariables = new string[]
      {
        EnvironmentVariableKeys.NyxDbConnection,
        EnvironmentVariableKeys.PostizApiKey,
        EnvironmentVariableKeys.PostizBasePublicV1,
        EnvironmentVariableKeys.ElevenLabsKey,
        EnvironmentVariableKeys.ElevenLabsVoiceId
      };

    internal static string ElevenLabsClientName = "ElevenLabsClient";
    internal static string PostizClientName = "PostizClient";


    static async Task Main(string[] args)
    {
      HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
      builder.Configuration.AddEnvironmentVariables();

      var cs = Environment.GetEnvironmentVariable(EnvironmentVariableKeys.NyxDbConnection)
               ?? builder.Configuration[EnvironmentVariableKeys.NyxDbConnection]
               ?? "";

      VerifyRequiredEnvVars(builder.Configuration, RequiredEnvVariables);

      builder.Services.AddDbContextFactory<NyxDbContext>(opt =>
      {
        if (string.IsNullOrWhiteSpace(cs))
          throw new InvalidOperationException($"{EnvironmentVariableKeys.NyxDbConnection} env var is required.");

        opt.UseSqlServer(cs, sql =>
        {
          sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        });
      });

      builder.Services.AddHttpClient(ElevenLabsClientName, (sp, client) =>
      {
        client.BaseAddress = new Uri("https://api.elevenlabs.io/");
        client.Timeout = TimeSpan.FromMinutes(2);

        // Auth
        var apiKey = builder.Configuration[EnvironmentVariableKeys.ElevenLabsKey] ?? "";
        if (string.IsNullOrWhiteSpace(apiKey))
          throw new InvalidOperationException($"{EnvironmentVariableKeys.ElevenLabsKey} env var is required.");

        client.DefaultRequestHeaders.Remove("xi-api-key");
        client.DefaultRequestHeaders.Add("xi-api-key", apiKey);

        // Accept anything (audio bytes)
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
      });


      builder.Services.AddHttpClient(PostizClientName, (sp, client) =>
      {
        // Load from env
        var baseUrl = Environment.GetEnvironmentVariable(EnvironmentVariableKeys.PostizBasePublicV1) ?? "";
        if (string.IsNullOrWhiteSpace(baseUrl))
          throw new InvalidOperationException($"{EnvironmentVariableKeys.PostizBasePublicV1} env var is required.");

        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromMinutes(10); // uploads can be big as working with viedos
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        // API key header (Postiz expects Authorization: <key>)
        var apiKey = builder.Configuration[EnvironmentVariableKeys.PostizApiKey] ?? "";
        if (string.IsNullOrWhiteSpace(apiKey))
          throw new InvalidOperationException($"{EnvironmentVariableKeys.PostizApiKey} env var is required.");

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
      builder.Services.AddHostedService<GeneratedAudioWorker>();
      builder.Services.AddHostedService<GeneratedRenderWorker>();
      //builder.Services.AddHostedService<GeneratedThumbnailWorker>(); -- Cant get it to work
      builder.Services.AddHostedService<PremadeThumbnailWorker>();

      string? premadeRoot = builder.Configuration[EnvironmentVariableKeys.PremadeRoot];
      if (!string.IsNullOrEmpty(premadeRoot))
      {
        if (!Directory.Exists(premadeRoot))
          Directory.CreateDirectory(premadeRoot);

        builder.Services.AddHostedService<PremadeSplitterWorker>();
        Console.WriteLine($"PremadeSplitterWorker enabled. Root: {premadeRoot}");
      }
      else
        // Log that no path has been provided
        Console.WriteLine($"No {EnvironmentVariableKeys.PremadeRoot} provided; PremadeSplitterWorker will not start.");

      var app = builder.Build();
      await ApplyMigrationsWithRetryAsync(app.Services);
      await app.RunAsync();
    }


    static void VerifyRequiredEnvVars(ConfigurationManager manager, params string[] keys)
    {
      var missing = keys.Where(k => string.IsNullOrWhiteSpace(manager[k])).ToList();
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