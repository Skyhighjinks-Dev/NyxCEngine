using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NyxCEngine.Database;
using NyxCEngine.Database.Tables;
using NyxCEngine.Util;
using NyxCEngine.Util.Thumbnails;

namespace NyxCEngine.Services
{
  public sealed class GeneratedThumbnailWorker : BackgroundService
  {
    private readonly IServiceProvider _sp;
    private readonly ILogger<GeneratedThumbnailWorker> _log;

    public GeneratedThumbnailWorker(IServiceProvider sp, ILogger<GeneratedThumbnailWorker> log)
    {
      _sp = sp;
      _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        try { await RunOnce(stoppingToken); }
        catch (Exception ex) { _log.LogError(ex, "GeneratedThumbnailWorker cycle failed"); }

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
          x.ThumbnailPath == null &&
          x.Mp4Path != null,
          ct);

      if (asset is null) return;

      if (string.IsNullOrWhiteSpace(asset.Mp4Path) || !File.Exists(asset.Mp4Path))
      {
        _log.LogError("Thumb skipped: MP4 missing for VideoAssetId={Id}. Mp4Path={Mp4}", asset.Id, asset.Mp4Path);
        return;
      }

      string? scriptText = null;
      if (!string.IsNullOrWhiteSpace(asset.ScriptFilePath) && File.Exists(asset.ScriptFilePath))
        scriptText = (await File.ReadAllTextAsync(asset.ScriptFilePath!, ct)).Trim();

      // ---- TEXT CHOICE (Generated) ----
      var raw = ExtractFirstSentence(scriptText) ?? "NIGHTSHIFT";
      raw = FfmpegThumbnailRenderer.Stylize(raw);

      // One word per line (max 4 lines) -> guaranteed fit, very "TikTok style"
      var text = FfmpegThumbnailRenderer.OneWordPerLine(raw, maxLines: 4);

      // Big + bold, but not insane (prevents clipping even with outline/box)
      var fontSize = FfmpegThumbnailRenderer.ChooseFontSizeForText(
        text,
        big: 170,
        medium: 150,
        small: 130,
        tiny: 115);

      var font = (Environment.GetEnvironmentVariable(EnvironmentVariableKeys.PremadeThumbFontPath) ?? "").Trim();
      if (string.IsNullOrWhiteSpace(font))
        font = @"C:\Windows\Fonts\arialbd.ttf";

      var dir = Path.GetDirectoryName(asset.Mp4Path)!;
      var thumbPath = Path.Combine(dir, $"thumb_{asset.Id:000000}.jpg");

      // Frame: 35% in, min 1s
      var dur = FfmpegThumbnailRenderer.ProbeDurationSeconds(asset.Mp4Path!);
      var ts = Math.Max(1.0, dur * 0.35);

      _log.LogInformation("Generating GENERATED thumbnail for VideoAssetId={Id} font={FontSize} text={Text}",
        asset.Id, fontSize, text.Replace("\n", " / "));

      FfmpegThumbnailRenderer.RenderCenteredTextThumb(
        inputVideoPath: asset.Mp4Path!,
        outputJpgPath: thumbPath,
        text: text,
        timestampSeconds: ts,
        fontFilePath: font,
        fontSize: fontSize,
        overlayDarkness: 0.42,
        borderW: 12
      );

      asset.ThumbnailPath = thumbPath;
      await db.SaveChangesAsync(ct);

      _log.LogInformation("Generated thumbnail created for VideoAssetId={Id}: {Thumb}", asset.Id, thumbPath);
    }

    private static string? ExtractFirstSentence(string? text)
    {
      if (string.IsNullOrWhiteSpace(text))
        return null;

      text = text.Replace("\r\n", "\n").Trim();

      var idx = text.IndexOfAny(new[] { '.', '!', '?' });
      var candidate = idx >= 0 ? text.Substring(0, idx + 1) : (text.Split('\n').FirstOrDefault() ?? text);

      candidate = candidate.Trim().Trim('"', '\'', ' ');
      if (candidate.Length == 0) return null;

      const int max = 90;
      if (candidate.Length > max)
        candidate = candidate.Substring(0, max).TrimEnd() + "…";

      return candidate;
    }
  }
}
