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
  public sealed class PremadeThumbnailWorker : BackgroundService
  {
    private readonly IServiceProvider _sp;
    private readonly ILogger<PremadeThumbnailWorker> _log;

    public PremadeThumbnailWorker(IServiceProvider sp, ILogger<PremadeThumbnailWorker> log)
    {
      _sp = sp;
      _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        try { await RunOnce(stoppingToken); }
        catch (Exception ex) { _log.LogError(ex, "PremadeThumbnailWorker cycle failed"); }

        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
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
          x.SourceType == VideoAsset.VideoAssetSourceType.PremadeSegment &&
          x.ThumbnailPath == null &&
          x.Mp4Path != null &&
          x.SeriesIndex != null &&
          x.SeriesCount != null,
          ct);

      if (asset is null) return;

      if (string.IsNullOrWhiteSpace(asset.Mp4Path) || !File.Exists(asset.Mp4Path))
      {
        _log.LogError("Premade thumb skipped: MP4 missing for VideoAssetId={Id}. Mp4Path={Mp4}", asset.Id, asset.Mp4Path);
        return;
      }

      // ---- TEXT CHOICE (Premade) ----
      var partText = (asset.SeriesIndex == asset.SeriesCount) ? "FINAL PART" : $"PART {asset.SeriesIndex}";
      partText = FfmpegThumbnailRenderer.Stylize(partText);
      //partText = FfmpegThumbnailRenderer.WrapToTwoLines(partText, maxCharsPerLine: 12);

      var fontSize = FfmpegThumbnailRenderer.ChooseFontSizeForText(
        partText, big: 190, medium: 170, small: 150, tiny: 130);

      var font = (Environment.GetEnvironmentVariable(EnvironmentVariableKeys.PremadeThumbFontPath) ?? "").Trim();
      if (string.IsNullOrWhiteSpace(font))
        font = @"C:\Windows\Fonts\arialbd.ttf";

      var dir = Path.GetDirectoryName(asset.Mp4Path)!;
      var thumbPath = Path.Combine(dir, $"thumb_part_{asset.SeriesIndex:000}.jpg");

      // Frame: 20% in, min 1s
      var dur = FfmpegThumbnailRenderer.ProbeDurationSeconds(asset.Mp4Path!);
      var ts = Math.Max(1.0, dur * 0.20);

      _log.LogInformation("Generating PREMADE thumbnail for VideoAssetId={Id} font={FontSize} text={Text}",
        asset.Id, fontSize, partText.Replace("\n", " / "));

      FfmpegThumbnailRenderer.RenderCenteredTextThumb(
        inputVideoPath: asset.Mp4Path!,
        outputJpgPath: thumbPath,
        text: partText,
        timestampSeconds: ts,
        fontFilePath: font,
        fontSize: fontSize,
        overlayDarkness: 0.45,
        borderW: 14
      );

      asset.ThumbnailPath = thumbPath;
      await db.SaveChangesAsync(ct);

      _log.LogInformation("Premade thumbnail created for VideoAssetId={Id}: {Thumb}", asset.Id, thumbPath);
    }
  }
}