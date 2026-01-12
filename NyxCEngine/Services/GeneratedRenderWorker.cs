using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NyxCEngine.Database;
using NyxCEngine.Database.Tables;
using NyxCEngine.Util;
using NyxCEngine.Util.Captions;
using System.Globalization;

namespace NyxCEngine.Services
{
  public sealed class GeneratedRenderWorker : BackgroundService
  {
    private readonly IServiceProvider _sp;
    private readonly ILogger<GeneratedRenderWorker> _log;

    // Lead-in + tail hold
    private const double LeadInSeconds = 1.0;

    public GeneratedRenderWorker(IServiceProvider sp, ILogger<GeneratedRenderWorker> log)
    {
      _sp = sp;
      _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          await RunOnce(stoppingToken);
        }
        catch (Exception ex)
        {
          _log.LogError(ex, "GeneratedRenderWorker cycle failed");
        }

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
      }
    }

    private async Task RunOnce(CancellationToken ct)
    {
      using var scope = _sp.CreateScope();
      var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NyxDbContext>>();
      await using var db = await dbFactory.CreateDbContextAsync(ct);

      var asset = await db.VideoAssets
        .OrderBy(x => x.CreatedAtUtc)
        .FirstOrDefaultAsync(x =>
          x.SourceType == VideoAsset.VideoAssetSourceType.Generated &&
          x.WavPath != null &&
          x.TimestampsPath != null &&
          x.BackgroundFilePath == null,
          ct);

      if (asset is null)
        return;

      if (!File.Exists(asset.WavPath!) || !File.Exists(asset.TimestampsPath!))
      {
        _log.LogError(
          "Render skipped: missing WAV or timestamps for VideoAssetId={Id}. Wav={Wav} Ts={Ts}",
          asset.Id, asset.WavPath, asset.TimestampsPath);
        return;
      }

      // Decide background:
      // - If Mp4Path points to an existing file, treat it as an explicit background override.
      // - Otherwise pick one from the backgrounds pool and set Mp4Path to it (so we can see what was chosen).
      var backgroundPath = await ResolveBackgroundPathAsync(asset, ct);
      if (string.IsNullOrWhiteSpace(backgroundPath) || !File.Exists(backgroundPath))
      {
        _log.LogError(
          "Render failed: no background available for VideoAssetId={Id}. CustomerId={CustomerId}. Mp4Path={Mp4Path}",
          asset.Id, asset.CustomerId, asset.Mp4Path);
        return;
      }

      var timestampsJson = await File.ReadAllTextAsync(asset.TimestampsPath!, ct);

      var style = new AssCaptionRenderer.CaptionStyle();
      var words = AssCaptionRenderer.ParseWordsFromTimestampsJson(timestampsJson);
      if (words.Count == 0)
      {
        _log.LogError("Render failed: no words parsed from timestamps for VideoAssetId={Id}", asset.Id);
        return;
      }

      // Shift captions forward to match delayed audio
      var chunks = AssCaptionRenderer.ChunkOneWord(words, style, LeadInSeconds);

      var baseDir = Path.GetDirectoryName(asset.ScriptFilePath ?? asset.WavPath!)!;
      var assPath = Path.Combine(baseDir, $"captions_{asset.Id:000000}.ass");
      var ass = AssCaptionRenderer.GenerateAss(chunks, style);
      await File.WriteAllTextAsync(assPath, ass, ct);

      var audioDuration = AssCaptionRenderer.ProbeDurationSeconds(asset.WavPath!);
      var bgDuration = AssCaptionRenderer.ProbeDurationSeconds(backgroundPath);

      // End buffer seconds (Python default 10)
      var endBufferEnv = Environment.GetEnvironmentVariable(EnvironmentVariableKeys.BackgroundVideoEndBuffer);
      var endBufferSeconds = 10.0;
      if (!string.IsNullOrWhiteSpace(endBufferEnv) &&
          double.TryParse(endBufferEnv, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedBuf) &&
          parsedBuf >= 0)
      {
        endBufferSeconds = parsedBuf;
      }

      double startTime;
      bool needsLoop;

      if (asset.BackgroundStartOffsetSeconds is double explicitOffset)
      {
        startTime = explicitOffset;

        var required = (audioDuration + LeadInSeconds) + endBufferSeconds;
        if (bgDuration <= startTime + required)
        {
          _log.LogWarning(
            "Background too short for explicit offset. VideoAssetId={Id}. bg={Bg:0.00}s start={Start:0.00}s required={Req:0.00}s. Falling back to looping from 0.",
            asset.Id, bgDuration, startTime, required);

          startTime = 0.0;
          needsLoop = true;
        }
        else
        {
          needsLoop = false;
        }
      }
      else
      {
        var maxStart = bgDuration - ((audioDuration + LeadInSeconds) + endBufferSeconds);

        if (maxStart > 0)
        {
          startTime = Random.Shared.NextDouble() * maxStart;
          needsLoop = false;
        }
        else
        {
          startTime = 0.0;
          needsLoop = true;
        }
      }

      var outputPath = Path.Combine(baseDir, $"rendered_{asset.Id:000000}.mp4");

      _log.LogInformation(
        "Rendering VideoAssetId={Id} bg={Bg} bgDur={BgDur:0.00}s start={Start:0.00}s loop={Loop} audioDur={Audio:0.00}s leadIn={Lead:0.00}s endBuffer={Buf:0.00}s -> {Out}",
        asset.Id, backgroundPath, bgDuration, startTime, needsLoop, audioDuration, LeadInSeconds, endBufferSeconds, outputPath);

      AssCaptionRenderer.Render(
        backgroundPath: backgroundPath,
        audioWavPath: asset.WavPath!,
        assPath: assPath,
        outputPath: outputPath,
        durationSeconds: audioDuration,
        startTimeSeconds: startTime,
        needsLoop: needsLoop,
        audioDelaySeconds: LeadInSeconds
      );

      asset.BackgroundFilePath = backgroundPath;
      asset.BackgroundStartOffsetSeconds = startTime;
      asset.EndBufferSecondsUsed = endBufferSeconds;
      asset.Mp4Path = outputPath;

      await db.SaveChangesAsync(ct);

      _log.LogInformation("Rendered VideoAssetId={Id} complete. Mp4Path now {Mp4Path}", asset.Id, asset.Mp4Path);
    }

    private async Task<string?> ResolveBackgroundPathAsync(VideoAsset asset, CancellationToken ct)
    {
      // If Mp4Path already exists on disk, treat it as an explicit override.
      if (!string.IsNullOrWhiteSpace(asset.Mp4Path) && File.Exists(asset.Mp4Path))
      {
        return asset.Mp4Path;
      }

      var root = (Environment.GetEnvironmentVariable(EnvironmentVariableKeys.BackgroundsRoot) ?? "").Trim();
      if (string.IsNullOrWhiteSpace(root))
      {
        _log.LogError("Missing env var: {Key}. Cannot pick backgrounds automatically.", EnvironmentVariableKeys.BackgroundsRoot);
        return null;
      }

      var customerDir = Path.Combine(root, asset.CustomerId);
      var defaultDir = Path.Combine(root, "default");

      var candidates = new List<string>();

      if (Directory.Exists(customerDir))
        candidates.AddRange(Directory.EnumerateFiles(customerDir, "*.mp4", SearchOption.TopDirectoryOnly));

      if (candidates.Count == 0 && Directory.Exists(defaultDir))
        candidates.AddRange(Directory.EnumerateFiles(defaultDir, "*.mp4", SearchOption.TopDirectoryOnly));

      if (candidates.Count == 0)
      {
        _log.LogError(
          "No background MP4s found. Looked in: {CustomerDir} then {DefaultDir}",
          customerDir, defaultDir);
        return null;
      }

      // Random selection
      var chosen = candidates[Random.Shared.Next(0, candidates.Count)];

      _log.LogInformation(
        "Selected background for VideoAssetId={Id}: {Chosen} (candidates={Count}, customerDir={CustomerDir})",
        asset.Id, chosen, candidates.Count, customerDir);

      // Persist choice into Mp4Path so you can see exactly what was used even before render completes.
      // This also makes the run deterministic if it crashes mid-render and restarts.
      asset.Mp4Path = chosen;

      // Save immediately so if we crash mid render, we don't keep picking different backgrounds.
      // (No transaction needed; render completion is still controlled by BackgroundFilePath null/not-null.)
      using var scope = _sp.CreateScope();
      var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NyxDbContext>>();
      await using var db = await dbFactory.CreateDbContextAsync(ct);

      // Attach minimal update
      db.Attach(asset);
      db.Entry(asset).Property(x => x.Mp4Path).IsModified = true;
      await db.SaveChangesAsync(ct);

      return chosen;
    }
  }
}