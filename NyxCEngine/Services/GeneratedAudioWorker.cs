using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NyxCEngine.APIs.ElevenLabs;
using NyxCEngine.APIs.ElevenLabs.Models;
using NyxCEngine.Database;
using NyxCEngine.Database.Tables;
using NyxCEngine.Util.Audio;

namespace NyxCEngine.Services
{
  /// <summary>
  /// Generated pipeline stage (single atomic call like Python):
  /// ScriptFilePath -> ElevenLabs (PCM + timestamps) -> WAV + timestamps.json -> store WavPath + TimestampsPath + AudioDurationSeconds.
  /// </summary>
  public sealed class GeneratedAudioWorker : BackgroundService
  {
    private readonly IServiceProvider _sp;
    private readonly ILogger<GeneratedAudioWorker> _log;

    public GeneratedAudioWorker(IServiceProvider sp, ILogger<GeneratedAudioWorker> log)
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
          _log.LogError(ex, "GeneratedAudioWorker cycle failed");
        }

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
      }
    }

    private static int SampleRateFromOutputFormat(string outputFormat)
    {
      // ElevenLabs output format looks like "pcm_24000"
      if (outputFormat.StartsWith("pcm_", StringComparison.OrdinalIgnoreCase) &&
          int.TryParse(outputFormat.Substring(4), out var sr) &&
          sr > 0)
        return sr;

      return 24000;
    }

    private async Task RunOnce(CancellationToken ct)
    {
      using var scope = _sp.CreateScope();

      var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NyxDbContext>>();
      await using var db = await dbFactory.CreateDbContextAsync(ct);

      var eleven = scope.ServiceProvider.GetRequiredService<ElevenLabsEngine>();

      // Find the next generated asset needing TTS+timestamps
      // We key off TimestampsPath so we don’t re-run if WAV already exists.
      var asset = await db.VideoAssets
        .OrderBy(x => x.CreatedAtUtc)
        .FirstOrDefaultAsync(x =>
          x.SourceType == VideoAsset.VideoAssetSourceType.Generated &&
          x.ScriptFilePath != null &&
          x.TimestampsPath == null,
          ct);

      if (asset is null)
        return;

      if (string.IsNullOrWhiteSpace(asset.ScriptFilePath) || !File.Exists(asset.ScriptFilePath))
      {
        _log.LogError("Generated TTS failed: Script file missing for VideoAssetId={Id}. Path={Path}",
          asset.Id, asset.ScriptFilePath);
        return;
      }

      var text = (await File.ReadAllTextAsync(asset.ScriptFilePath, ct)).Trim();
      if (string.IsNullOrWhiteSpace(text))
      {
        _log.LogError("Generated TTS failed: Script file empty for VideoAssetId={Id}. Path={Path}",
          asset.Id, asset.ScriptFilePath);
        return;
      }

      _log.LogInformation("Generating ElevenLabs TTS+timestamps for VideoAssetId={Id}", asset.Id);

      // ONE CALL: returns PCM + alignment timestamps
      var (pcm, alignment, outputFormat) = await eleven.TextToSpeechWithTimestampsPcmAsync(text, ct);
      var sampleRateHz = SampleRateFromOutputFormat(outputFormat);

      // Paths next to script (consistent + easy to debug)
      var scriptDir = Path.GetDirectoryName(asset.ScriptFilePath)!;
      var wavPath = asset.WavPath ?? Path.Combine(scriptDir, $"audio_{asset.Id:000000}.wav");
      var tsPath = Path.Combine(scriptDir, $"timestamps_{asset.Id:000000}.json");

      // Overwrite WAV to guarantee it matches the returned timestamps
      WavWriter.WritePcm16Mono(wavPath, pcm, sampleRateHz);

      // Save timestamps in python-compatible shape: {"alignment": {...}}
      var saved = new ElevenLabsSavedTimestamps { Alignment = alignment };
      await File.WriteAllTextAsync(tsPath, JsonConvert.SerializeObject(saved, Formatting.Indented), ct);

      asset.WavPath = wavPath;
      asset.TimestampsPath = tsPath;

      // Prefer alignment duration if possible
      if (alignment.CharacterEndTimesSeconds.Count > 0)
        asset.AudioDurationSeconds = alignment.CharacterEndTimesSeconds[^1];
      else
        asset.AudioDurationSeconds = WavWriter.EstimateDurationSecondsPcm16Mono(pcm.Length, sampleRateHz);

      await db.SaveChangesAsync(ct);

      _log.LogInformation("Generated WAV+timestamps for VideoAssetId={Id}. Wav={WavPath}. Ts={TsPath}. Duration={DurationSeconds:0.00}s",
        asset.Id, wavPath, tsPath, asset.AudioDurationSeconds);
    }
  }
}