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

    // 1s lead-in (audio starts at 1s) AND tail hold (video runs 1s longer) via Render() duration extension.
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

      // "Ready to render" rule:
      // - Generated
      // - Has WAV + timestamps
      // - Not rendered yet (BackgroundFilePath == null is our render-complete marker)
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

      // For now, we treat Mp4Path as the background source input until you formalize background selection.
      var backgroundPath = asset.Mp4Path;
      if (string.IsNullOrWhiteSpace(backgroundPath) || !File.Exists(backgroundPath))
      {
        _log.LogError(
          "Render failed: background video missing for VideoAssetId={Id}. Mp4Path={Mp4Path}",
          asset.Id, asset.Mp4Path);
        return;
      }

      var timestampsJson = await File.ReadAllTextAsync(asset.TimestampsPath!, ct);

      // Parse alignment -> words -> one-word chunks (same behavior as Python prototype)
      var style = new AssCaptionRenderer.CaptionStyle();
      var words = AssCaptionRenderer.ParseWordsFromTimestampsJson(timestampsJson);
      if (words.Count == 0)
      {
        _log.LogError("Render failed: no words parsed from timestamps for VideoAssetId={Id}", asset.Id);
        return;
      }

      // Shift captions forward by the lead-in so they match delayed audio.
      var chunks = AssCaptionRenderer.ChunkOneWord(words, style, LeadInSeconds);

      // Write ASS file next to the script (or next to WAV if script is null) for easy debugging
      var baseDir = Path.GetDirectoryName(asset.ScriptFilePath ?? asset.WavPath!)!;
      var assPath = Path.Combine(baseDir, $"captions_{asset.Id:000000}.ass");
      var ass = AssCaptionRenderer.GenerateAss(chunks, style);
      await File.WriteAllTextAsync(assPath, ass, ct);

      // Duration from WAV (ffprobe)
      var audioDuration = AssCaptionRenderer.ProbeDurationSeconds(asset.WavPath!);

      // Background duration (ffprobe)
      var bgDuration = AssCaptionRenderer.ProbeDurationSeconds(backgroundPath);

      // End buffer seconds (Python default is 10)
      var endBufferEnv = Environment.GetEnvironmentVariable(EnvironmentVariableKeys.BackgroundVideoEndBuffer);
      var endBufferSeconds = 10.0;
      if (!string.IsNullOrWhiteSpace(endBufferEnv) &&
          double.TryParse(endBufferEnv, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedBuf) &&
          parsedBuf >= 0)
      {
        endBufferSeconds = parsedBuf;
      }

      // Background offset + looping logic:
      // - If explicit offset exists in DB, respect it (unless unsafe).
      // - Else random offset if possible; else loop.
      double startTime;
      bool needsLoop;

      if (asset.BackgroundStartOffsetSeconds is double explicitOffset)
      {
        startTime = explicitOffset;

        // Safety validation: bg must cover start + (audio + lead-in) + endBuffer
        var required = (audioDuration + LeadInSeconds) + endBufferSeconds;
        if (bgDuration <= startTime + required)
        {
          _log.LogWarning(
            "Background too short for explicit offset. VideoAssetId={Id}. bg={Bg:0.00}s start={Start:0.00}s required={Req:0.00}s (audio+lead+buffer). Falling back to looping from 0.",
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
        // Need room for (audio + lead-in) plus end buffer.
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

      // Output rendered MP4 path
      var outputPath = Path.Combine(baseDir, $"rendered_{asset.Id:000000}.mp4");

      _log.LogInformation(
        "Rendering VideoAssetId={Id} bgDur={BgDur:0.00}s start={Start:0.00}s loop={Loop} audioDur={Audio:0.00}s leadIn={Lead:0.00}s endBuffer={Buf:0.00}s -> {Out}",
        asset.Id, bgDuration, startTime, needsLoop, audioDuration, LeadInSeconds, endBufferSeconds, outputPath);

      // Render final mp4 (Render() will:
      // - delay audio by LeadInSeconds (adelay)
      // - extend total duration by LeadInSeconds (tail hold)
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

      // Update DB: lock in background + offset used + end buffer, and point Mp4Path to rendered output
      asset.BackgroundFilePath = backgroundPath;
      asset.BackgroundStartOffsetSeconds = startTime;
      asset.EndBufferSecondsUsed = endBufferSeconds;
      asset.Mp4Path = outputPath;

      await db.SaveChangesAsync(ct);

      _log.LogInformation("Rendered VideoAssetId={Id} complete. Mp4Path now {Mp4Path}", asset.Id, asset.Mp4Path);
    }
  }
}
