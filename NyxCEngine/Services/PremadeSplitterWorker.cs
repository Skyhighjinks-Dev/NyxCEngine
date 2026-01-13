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
    private readonly string _workerId = Environment.MachineName;
    private readonly string _premadeRoot;

    public PremadeSplitterWorker(IServiceProvider sp, ILogger<PremadeSplitterWorker> log, IConfiguration config)
    {
      _sp = sp;
      _log = log;
      _premadeRoot = config[EnvironmentVariableKeys.PremadeRoot] ?? "";

      if (!string.IsNullOrWhiteSpace(_premadeRoot))
        Directory.CreateDirectory(_premadeRoot);
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

      var lockExpiry = DateTime.UtcNow.AddMinutes(-10);

      var series = await db.PremadeVideoSeries
          .Where(x => x.Status == PremadeSeriesStatus.PendingSplit)
          .Where(x => x.LockedAtUtc == null || x.LockedAtUtc < lockExpiry)
          .OrderBy(x => x.CreatedAtUtc)
          .FirstOrDefaultAsync(ct);

      if (series is null) return;

      // claim
      series.LockedAtUtc = DateTime.UtcNow;
      series.LockOwner = _workerId;
      await db.SaveChangesAsync(ct);

      if (!File.Exists(series.SourcePath))
      {
        series.Status = PremadeSeriesStatus.Failed;
        await db.SaveChangesAsync(ct);
        _log.LogError("Source video missing: {Path}", series.SourcePath);
        return;
      }

      var outDir = Path.Combine(Path.GetDirectoryName(series.SourcePath)!, "segments", series.Id.ToString("N"));
      Directory.CreateDirectory(outDir);

      // IMPORTANT: use 1-based numbering to match PART 1/2/3
      var outPattern = Path.Combine(outDir, "part_%03d.mp4");

      // run ffmpeg split
      await FfmpegSplitAsync(series.SourcePath, series.SegmentSeconds, outPattern, ct);

      // Collect parts
      var parts = Directory.GetFiles(outDir, "part_*.mp4").OrderBy(p => p).ToList();
      if (parts.Count == 0)
      {
        series.Status = PremadeSeriesStatus.Failed;
        await db.SaveChangesAsync(ct);
        _log.LogError("Split produced no parts: {SeriesId}", series.Id);
        return;
      }

      // ✅ Merge remainder logic:
      // If the last part is shorter than SegmentSeconds, merge it onto the previous one.
      // Example: 60s segments with 30s remainder => last becomes 90s and part count decreases by 1.
      if (parts.Count >= 2)
      {
        var last = parts[^1];
        var lastDur = await FfprobeDurationSecondsAsync(last, ct);

        // allow tiny tolerance due to container timestamps
        if (lastDur < (series.SegmentSeconds - 0.25))
        {
          var prev = parts[^2];

          _log.LogInformation(
            "Remainder detected (last part {LastDur:0.00}s < {Seg}s). Merging {Prev} + {Last}",
            lastDur, series.SegmentSeconds, prev, last);

          var mergedTemp = Path.Combine(outDir, "merged_tmp.mp4");
          await FfmpegConcatTwoAsync(prev, last, mergedTemp, ct);

          // Replace prev with merged (keep filename stable)
          File.Delete(prev);
          File.Delete(last);
          File.Move(mergedTemp, prev);

          // Rebuild parts list and renumber to keep part_001..part_N continuity
          parts = Directory.GetFiles(outDir, "part_*.mp4").OrderBy(p => p).ToList();
          parts = await RenumberPartsAsync(outDir, parts, ct);
        }
      }

      var count = parts.Count;

      // Optional safety: clear any old assets for this series if rerun
      var existing = await db.VideoAssets
        .Where(v => v.SourceType == VideoAssetSourceType.PremadeSegment && v.SeriesId == series.Id)
        .ToListAsync(ct);
      if (existing.Count > 0)
      {
        db.VideoAssets.RemoveRange(existing);
        await db.SaveChangesAsync(ct);
      }

      for (int i = 0; i < count; i++)
      {
        var isFinal = (i == count - 1);
        var label = isFinal ? "FINAL PART" : $"PART {i + 1}";

        var thumbPath = Path.Combine(outDir, $"thumb_part_{(i + 1):000}.jpg");
        await FfmpegThumbnailAsync(parts[i], thumbPath, label, ct);

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

    private static async Task<List<string>> RenumberPartsAsync(string outDir, List<string> parts, CancellationToken ct)
    {
      // Renumber to part_001.mp4, part_002.mp4 ... in lexical order
      var tempDir = Path.Combine(outDir, "_renumber_tmp");
      Directory.CreateDirectory(tempDir);

      // Move to temp names first to avoid collisions
      for (int i = 0; i < parts.Count; i++)
      {
        var tmp = Path.Combine(tempDir, $"tmp_{(i + 1):000}.mp4");
        File.Move(parts[i], tmp, overwrite: true);
      }

      // Move back with correct names
      var newParts = new List<string>();
      for (int i = 0; i < parts.Count; i++)
      {
        var tmp = Path.Combine(tempDir, $"tmp_{(i + 1):000}.mp4");
        var final = Path.Combine(outDir, $"part_{(i + 1):000}.mp4");
        File.Move(tmp, final, overwrite: true);
        newParts.Add(final);
      }

      Directory.Delete(tempDir, recursive: true);
      await Task.CompletedTask;
      return newParts;
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

      // Keep re-encode off (fast split)
      psi.ArgumentList.Add("-c");
      psi.ArgumentList.Add("copy");

      psi.ArgumentList.Add("-map");
      psi.ArgumentList.Add("0");

      psi.ArgumentList.Add("-f");
      psi.ArgumentList.Add("segment");

      // 1-based numbering so part_001 aligns with PART 1
      psi.ArgumentList.Add("-segment_start_number");
      psi.ArgumentList.Add("1");

      psi.ArgumentList.Add("-segment_time");
      psi.ArgumentList.Add(segmentSeconds.ToString(CultureInfo.InvariantCulture));

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

    private static async Task<double> FfprobeDurationSecondsAsync(string path, CancellationToken ct)
    {
      var psi = new ProcessStartInfo
      {
        FileName = "ffprobe",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
      };

      psi.ArgumentList.Add("-v");
      psi.ArgumentList.Add("error");
      psi.ArgumentList.Add("-show_entries");
      psi.ArgumentList.Add("format=duration");
      psi.ArgumentList.Add("-of");
      psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
      psi.ArgumentList.Add(path);

      using var p = Process.Start(psi)!;
      var stdoutTask = p.StandardOutput.ReadToEndAsync();
      var stderrTask = p.StandardError.ReadToEndAsync();

      await p.WaitForExitAsync(ct);

      var stdout = (await stdoutTask).Trim();
      var stderr = await stderrTask;

      if (p.ExitCode != 0)
        throw new Exception($"ffprobe failed ({p.ExitCode}):\n{stderr}");

      return double.Parse(stdout, CultureInfo.InvariantCulture);
    }

    private static async Task FfmpegConcatTwoAsync(string first, string second, string output, CancellationToken ct)
    {
      // concat demuxer list wants forward slashes to avoid Windows escaping pain
      static string ToConcatPath(string p)
        => p.Replace("\\", "/").Replace("'", "\\'");

      var listPath = Path.Combine(Path.GetDirectoryName(output)!, $"concat_{Guid.NewGuid():N}.txt");

      var lines = new[]
      {
        $"file '{ToConcatPath(first)}'",
        $"file '{ToConcatPath(second)}'",
      };

      await File.WriteAllLinesAsync(listPath, lines, ct);

      var psi = new ProcessStartInfo
      {
        FileName = "ffmpeg",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
      };

      psi.ArgumentList.Add("-hide_banner");
      psi.ArgumentList.Add("-y");
      psi.ArgumentList.Add("-f");
      psi.ArgumentList.Add("concat");
      psi.ArgumentList.Add("-safe");
      psi.ArgumentList.Add("0");
      psi.ArgumentList.Add("-i");
      psi.ArgumentList.Add(listPath);
      psi.ArgumentList.Add("-c");
      psi.ArgumentList.Add("copy");
      psi.ArgumentList.Add(output);

      using var p = Process.Start(psi)!;
      var stderrTask = p.StandardError.ReadToEndAsync();

      await p.WaitForExitAsync(ct);
      var stderr = await stderrTask;

      try { File.Delete(listPath); } catch { /* ignore */ }

      if (p.ExitCode != 0)
        throw new Exception($"ffmpeg concat failed ({p.ExitCode}):\n{stderr}");
    }

    private static async Task FfmpegThumbnailAsync(
      string inputMp4,
      string outputJpg,
      string label,
      CancellationToken ct)
    {
      // Use configurable font if present; fall back to Windows bold Arial.
      var font = (Environment.GetEnvironmentVariable(EnvironmentVariableKeys.PremadeThumbFontPath) ?? "").Trim();
      if (string.IsNullOrWhiteSpace(font))
        font = @"C:\Windows\Fonts\arialbd.ttf";

      static string EscapeDrawtext(string s)
      {
        // drawtext needs escaping for : and ' and \
        return (s ?? "")
          .Replace("\\", "\\\\")
          .Replace(":", "\\:")
          .Replace("'", "\\'");
      }

      static string EscapeFontPath(string path)
      {
        var p = path.Replace("\\", "/");
        p = p.Replace(":", "\\:");
        p = p.Replace("'", "\\'");
        return p;
      }

      var fontFfmpeg = EscapeFontPath(font);
      var safeLabel = EscapeDrawtext(label);

      // Make thumb 1080x1920, dark overlay, big centered text
      var vf =
        $"scale=1080:1920:force_original_aspect_ratio=increase," +
        $"crop=1080:1920," +
        $"drawbox=x=0:y=0:w=iw:h=ih:color=black@0.35:t=fill," +
        $"drawtext=fontfile='{fontFfmpeg}':" +
        $"text='{safeLabel}':" +
        $"fontcolor=white:fontsize=160:" +
        $"borderw=12:bordercolor=black:" +
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

      // grab a frame slightly in (0.5s) so it's not black
      psi.ArgumentList.Add("-ss");
      psi.ArgumentList.Add("0.5");

      psi.ArgumentList.Add("-i");
      psi.ArgumentList.Add(inputMp4);

      psi.ArgumentList.Add("-vframes");
      psi.ArgumentList.Add("1");

      psi.ArgumentList.Add("-vf");
      psi.ArgumentList.Add(vf);

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