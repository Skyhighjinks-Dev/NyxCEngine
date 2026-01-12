using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NyxCEngine.APIs.Postiz;
using NyxCEngine.APIs.Postiz.Models;
using NyxCEngine.APIs.Postiz.Models.Integrations;
using NyxCEngine.Database;
using NyxCEngine.Database.Tables;

namespace NyxCEngine.Services
{
  internal sealed class VideoPipelineWorker : BackgroundService
  {
    private readonly PostizEngine _postizEngine;
    private readonly NyxDbContext _db;
    private readonly ILogger<VideoPipelineWorker> _logger;

    public VideoPipelineWorker(PostizEngine postiz, NyxDbContext db, ILogger<VideoPipelineWorker> logger)
    {
      _postizEngine = postiz;
      _db = db;
      _logger = logger;
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
          _logger.LogError(ex, "VideoPipelineWorker cycle failed");
        }

        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
      }
    }

    private async Task RunOnce(CancellationToken ct)
    {
      // 1) Pick next eligible premade segment (enforce series ordering)
      //    - must be premade segment
      //    - must not already have a ScheduledPost
      //    - if SeriesIndex > 1, previous part must already have at least one ScheduledPost
      var next = await _db.VideoAssets
        .AsNoTracking()
        .Where(v => v.SourceType == VideoAsset.VideoAssetSourceType.PremadeSegment)
        .Where(v => v.SeriesId != null && v.SeriesIndex != null)
        .Where(v => !v.ScheduledPosts.Any())
        .Where(v =>
          v.SeriesIndex == 1 ||
          _db.VideoAssets.Any(p =>
            p.SeriesId == v.SeriesId &&
            p.SeriesIndex == v.SeriesIndex - 1 &&
            p.ScheduledPosts.Any()))
        .OrderBy(v => v.CreatedAtUtc)
        .ThenBy(v => v.SeriesId)
        .ThenBy(v => v.SeriesIndex)
        .FirstOrDefaultAsync(ct);

      if (next is null)
      {
        _logger.LogInformation("No eligible premade segments to schedule.");
        return;
      }

      if (!File.Exists(next.Mp4Path))
      {
        _logger.LogError("Segment file missing: {Path} (VideoAssetId={Id})", next.Mp4Path, next.Id);
        return;
      }

      // 2) Pick integration: forced by TargetIntegrationId if set; else pick first YouTube integration
      var integrations = await _postizEngine.ListIntegrationsAsync(ct);

      IntegrationDto? integration =
        !string.IsNullOrWhiteSpace(next.TargetIntegrationId)
          ? integrations.FirstOrDefault(i => i.Id == next.TargetIntegrationId)
          : integrations.FirstOrDefault(i => string.Equals(i.Identifier, "youtube", StringComparison.OrdinalIgnoreCase));

      if (integration is null)
        throw new InvalidOperationException("No suitable integration found for scheduling.");

      // 3) Decide schedule time.
      //    IMPORTANT: you have UNIQUE (IntegrationId, ScheduledAtUtc)
      //    so pick a time that is unlikely to collide; if it does collide, we detect it below.
      var scheduledAtUtc = DateTime.UtcNow.AddMinutes(5);
      var whenUtcIso = scheduledAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

      // If we've already scheduled something for this integration at that exact timestamp, bump by 1 minute.
      // (This is a simple collision-avoidance; you’ll replace this with your real slotting later.)
      var collision = await _db.ScheduledPosts
        .AsNoTracking()
        .AnyAsync(p => p.IntegrationId == integration.Id && p.ScheduledAtUtc == scheduledAtUtc, ct);

      if (collision)
      {
        scheduledAtUtc = scheduledAtUtc.AddMinutes(1);
        whenUtcIso = scheduledAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
      }

      // 4) Upload MP4
      var videoUpload = await _postizEngine.UploadMediaAsync(next.Mp4Path, ct);
      if (string.IsNullOrWhiteSpace(videoUpload.Id) || string.IsNullOrWhiteSpace(videoUpload.Path))
        throw new InvalidOperationException("Postiz video upload returned empty id/path.");

      // 5) Upload thumbnail if present (requires you to add ThumbnailPath to VideoAsset and populate it in splitter)
      UploadAssetDto? thumbUpload = null;
      if (!string.IsNullOrWhiteSpace(next.ThumbnailPath))
      {
        if (File.Exists(next.ThumbnailPath))
        {
          thumbUpload = await _postizEngine.UploadMediaAsync(next.ThumbnailPath, ct);
          if (string.IsNullOrWhiteSpace(thumbUpload.Id) || string.IsNullOrWhiteSpace(thumbUpload.Path))
            throw new InvalidOperationException("Postiz thumbnail upload returned empty id/path.");
        }
        else
        {
          _logger.LogWarning("ThumbnailPath set but file missing: {ThumbPath} (VideoAssetId={Id})", next.ThumbnailPath, next.Id);
        }
      }

      // 6) Build schedule payload
      var partNum = next.SeriesIndex ?? 1;

      object settings = new
      {
        __type = "youtube",
        title = $"Nightshift – Part {partNum}",
        type = "public",
        selfDeclaredMadeForKids = "no",
        thumbnail = thumbUpload is null ? null : new { id = thumbUpload.Id, path = thumbUpload.Path }
      };

      var req = new ScheduleBundleRequest
      {
        Type = "schedule",
        Date = whenUtcIso,
        ShortLink = false,
        Tags = new(),
        Posts = new()
        {
          new PostItemDto
          {
            Integration = new IntegrationRefWithIdentifier { Id = integration.Id },
            Value = new()
            {
              new PostValueDto
              {
                Content = $"PART {partNum} ✅",
                Image = new()
                {
                  new UploadRefDto { Id = videoUpload.Id!, Path = videoUpload.Path! }
                }
              }
            },
            Settings = settings
          }
        }
      };

      _logger.LogInformation("Schedule payload:\n{Json}", JsonConvert.SerializeObject(req, Formatting.Indented));

      // 7) Schedule in Postiz
      var scheduleResult = await _postizEngine.ScheduleBundleAsync(req, ct);
      var first = scheduleResult.FirstOrDefault();
      if (first is null || string.IsNullOrWhiteSpace(first.PostId))
        throw new InvalidOperationException("Postiz returned an empty schedule response (no postId).");

      // 8) Persist ScheduledPost (this is what enforces ordering for series parts)
      // NOTE: Platform is the provider identifier, e.g. "youtube"
      var platform = integration.Identifier ?? "youtube";

      _db.ScheduledPosts.Add(new ScheduledPost
      {
        CustomerId = next.CustomerId,
        Platform = platform,
        IntegrationId = integration.Id,
        ScheduledAtUtc = scheduledAtUtc,

        PostizPostId = first.PostId,
        PostizState = "scheduled",

        AssetId = next.Id,
        Status = "scheduled",

        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = null
      });

      await _db.SaveChangesAsync(ct);

      _logger.LogInformation(
        "✅ Scheduled SeriesId={SeriesId} Part={Part}/{Total} AssetId={AssetId} PostizPostId={PostizPostId} IntegrationId={IntegrationId} At={ScheduledAtUtc:o}",
        next.SeriesId, partNum, next.SeriesCount, next.Id, first.PostId, integration.Id, scheduledAtUtc);
    }
  }
}
