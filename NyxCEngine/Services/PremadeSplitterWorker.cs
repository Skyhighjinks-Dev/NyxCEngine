using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NyxCEngine.Database;
using NyxCEngine.Database.Tables;
using NyxCEngine.Util;
using System.Diagnostics;
using System.Globalization;
using static NyxCEngine.Database.Tables.PremadeVideoSeries;
using static NyxCEngine.Database.Tables.VideoAsset;

namespace NyxCEngine.Services
{
  public sealed class PremadeSplitterWorker : BackgroundService
  {
    private readonly IServiceProvider _sp;
    private readonly ILogger<PremadeSplitterWorker> _log;

    private readonly string _premadeRoot;
    private readonly string _lockOwner;
    private readonly string? _thumbFontPath;

    public PremadeSplitterWorker(IServiceProvider sp, ILogger<PremadeSplitterWorker> log, IConfiguration config)
    {
      _sp = sp;
      _log = log;

      _premadeRoot = config[EnvironmentVariableKeys.PremadeRoot] ?? "";
      if (!string.IsNullOrWhiteSpace(_premadeRoot))
        Directory.CreateDirectory(_premadeRoot);

      _lockOwner = $"PremadeSplitter:{Environment.MachineName}:{Guid.NewGuid():N}";
      if (_lockOwner.Length > 64)
        _lockOwner = _lockOwner[..64];

      var configuredFont = config[EnvironmentVariableKeys.PremadeThumbFontPath];

      if (!string.IsNullOrWhiteSpace(configuredFont))
      {
        _thumbFontPath = configuredFont;
      }
      else
      {
        _thumbFontPath = OperatingSystem.IsWindows()
          ? @"C:\Windows\Fonts\arialbd.ttf"
          : "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";
      }

      _log.LogInformation("PremadeSplitterWorker enabled. Root: {Root}", _premadeRoot);
      _log.LogInformation("PremadeSplitterWorker using thumb font: {FontPath}", _thumbFontPath);
      _log.LogInformation("PremadeSplitterWorker lock owner: {LockOwner}", _lockOwner);
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
          _log.LogError(ex, "PremadeSplitterWorker cycle failed");
        }

        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
      }
    }

    private async Task RunOnce(CancellationToken ct)
    {
      using var scope = _sp.CreateScope();
      var db = scope.ServiceProvider.GetRequiredService<NyxDbContext>();

      var nowUtc = DateTime.UtcNow;
      var lockExpiryUtc = nowUtc.AddMinutes(-10);

      // Atomic claim (EF + SQL Server locking hints)
      var claimed = await db.Set<ClaimedId>()
        .FromSqlInterpolated($@"
;WITH cte AS (
  SELECT TOP(1) *
  FROM dbo.premade_video_series WITH (UPDLOCK, READPAST, ROWLOCK)
  WHERE Status = {(int)PremadeSeriesStatus.PendingSplit}
    AND (LockedAtUtc IS NULL OR LockedAtUtc < {lockExpiryUtc})
  ORDER BY CreatedAtUtc
)
UPDATE cte
SET LockedAtUtc = {nowUtc},
    LockOwner = {_lockOwner}
OUTPUT INSERTED.Id AS Id;
")
        .AsNoTracking()
        .ToListAsync(ct);

      var claimedId = claimed.FirstOrDefault()?.Id;
      if (claimedId is null)
        return;

      var series = await db.PremadeVideoSeries.FirstAsync(x => x.Id == claimedId.Value, ct);

      if (!File.Exists(series.SourcePath))
      {
        series.Status = PremadeSeriesStatus.Failed;
        await db.SaveChangesAsync(ct);
        _log.LogError("Source video missing: {Path}", series.SourcePath);
        return;
      }

      var outDir = Path.Combine(Path.GetDirectoryName(series.SourcePath)!, "segments", series.Id.ToString("N"));
      Directory.CreateDirectory(outDir);

      var outPattern = Path.Combine(outDir, "part_%03d.mp4");

      // run ffmpeg split
      await FfmpegSplitAsync(series.SourcePath, series.SegmentSeconds, outPattern, ct);

      var parts = Directory.GetFiles(outDir, "part_*.mp4").OrderBy(p => p).ToList();
      if (parts.Count == 0)
      {
        series.Status = PremadeSeriesStatus.Failed;
        await db.SaveChangesAsync(ct);
        _log.LogError("Split produced no parts: {SeriesId}", series.Id);
        return;
      }

      var count = parts.Count;

      for (int i = 0; i < count; i++)
      {
        var thumbPath = Path.Combine(outDir, $"thumb_part_{(i + 1):000}.jpg");

        await FfmpegThumbnailAsync(
          inputMp4: parts[i],
          outputJpg: thumbPath,
          partLabel: $"PART {i + 1}",
          fontPath: _thumbFontPath,
          ct: ct);

        db.VideoAssets.Add(new VideoAsset
        {
          CustomerId = series.CustomerId,
          Mp4Path = parts[i],
          ThumbnailPath = thumbPath,
          SourceType = VideoAssetSourceType.PremadeSegment,
          SeriesId = series.Id,
          SeriesIndex = i + 1,
          SeriesCount = count,
          TargetIntegrationId = series.TargetIntegrationId,
          CreatedAtUtc = DateTime.UtcNow
        });
      }

      series.Status = PremadeSeriesStatus.SplitComplete;
      series.SplitAtUtc = DateTime.UtcNow;

      await db.SaveChangesAsync(ct);

      _log.LogInformation("Split series {SeriesId} into {Count} parts at {Dir}", series.Id, count, outDir);
    }

    private static async Task FfmpegSplitAsync(string input, int segmentSeconds, string outPattern, CancellationToken ct)
    {
      var psi = new ProcessStartInfo
      {
        FileName = "ffmpeg",
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false
      };

      psi.ArgumentList.Add("-hide_banner");
      psi.ArgumentList.Add("-y");
      psi.ArgumentList.Add("-i");
      psi.ArgumentList.Add(input);

      psi.ArgumentList.Add("-c");
      psi.ArgumentList.Add("copy");

      psi.ArgumentList.Add("-map");
      psi.ArgumentList.Add("0");

      psi.ArgumentList.Add("-f");
      psi.ArgumentList.Add("segment");

      psi.ArgumentList.Add("-segment_time");
      psi.ArgumentList.Add(segmentSeconds.ToString(CultureInfo.InvariantCulture));

      // IMPORTANT: start numbering at 1 => part_001.mp4 ...
      psi.ArgumentList.Add("-segment_start_number");
      psi.ArgumentList.Add("1");

      psi.ArgumentList.Add("-reset_timestamps");
      psi.ArgumentList.Add("1");

      psi.ArgumentList.Add(outPattern);

      using var p = Process.Start(psi)!;
      var stderrTask = p.StandardError.ReadToEndAsync();

      await p.WaitForExitAsync(ct);
      var stderr = await stderrTask;

      if (p.ExitCode != 0)
        throw new Exception($"ffmpeg split failed ({p.ExitCode}):\n{stderr}");
    }

    private static async Task FfmpegThumbnailAsync(
      string inputMp4,
      string outputJpg,
      string partLabel,
      string? fontPath,
      CancellationToken ct)
    {
      string? fontArg = null;

      if (!string.IsNullOrWhiteSpace(fontPath) && File.Exists(fontPath))
      {
        // FFmpeg drawtext wants forward slashes + escaped drive colon
        var fontFfmpeg = fontPath.Replace(@"\", "/").Replace(":", "\\:");
        fontArg = $"fontfile='{fontFfmpeg}':";
      }

      // escape single quotes for drawtext
      var safeText = partLabel.Replace("'", "\\'");

      var drawText =
        $"scale=1280:-2," +
        $"drawtext={fontArg}" +
        $"text='{safeText}':" +
        $"fontcolor=white:fontsize=96:" +
        $"borderw=6:bordercolor=black:" +
        $"x=(w-text_w)/2:y=(h-text_h)/2";

      var psi = new ProcessStartInfo
      {
        FileName = "ffmpeg",
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false
      };

      psi.ArgumentList.Add("-hide_banner");
      psi.ArgumentList.Add("-y");

      // avoid black first frame
      psi.ArgumentList.Add("-ss");
      psi.ArgumentList.Add("0.5");

      psi.ArgumentList.Add("-i");
      psi.ArgumentList.Add(inputMp4);

      psi.ArgumentList.Add("-vframes");
      psi.ArgumentList.Add("1");

      psi.ArgumentList.Add("-vf");
      psi.ArgumentList.Add(drawText);

      psi.ArgumentList.Add(outputJpg);

      using var p = Process.Start(psi)!;
      var stderrTask = p.StandardError.ReadToEndAsync();

      await p.WaitForExitAsync(ct);
      var stderr = await stderrTask;

      if (p.ExitCode != 0)
        throw new Exception($"ffmpeg thumbnail failed ({p.ExitCode}):\n{stderr}");
    }
  }
}