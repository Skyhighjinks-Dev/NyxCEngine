using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NyxCEngine.APIs.Postiz;
using NyxCEngine.APIs.Postiz.Models;
using NyxCEngine.APIs.Postiz.Models.Integrations;
using NyxCEngine.Database;

namespace NyxCEngine.Services
{
  internal class VideoPipelineWorker : BackgroundService
  {
    private readonly PostizEngine _postizEngine;
    private readonly ILogger<VideoPipelineWorker> _logger;

    public VideoPipelineWorker(PostizEngine postiz, ILogger<VideoPipelineWorker> logger)
    {
      this._postizEngine = postiz;
      this._logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      //do
      //{
        var testMp4Path = @"C:\nyx_temp\smoke.mp4";
        if (!File.Exists(testMp4Path))
          throw new FileNotFoundException("Smoke test mp4 not found", testMp4Path);

        // 1) Get integrations
        var integrations = await _postizEngine.ListIntegrationsAsync(stoppingToken);
        var integration = integrations.Where(x => string.Compare(x.Identifier, "youtube", true) == 0).FirstOrDefault();
        if (integration is null)
          throw new InvalidOperationException("No integrations returned by Postiz.");

        // 2) Upload mp4 to Postiz
        var upload = await _postizEngine.UploadMediaAsync(testMp4Path, stoppingToken);
        if (string.IsNullOrWhiteSpace(upload.Id) || string.IsNullOrWhiteSpace(upload.Path))
          throw new InvalidOperationException("Upload returned empty id/path.");

        // 3) Schedule bundle (use the DTOs you already have)
        var whenUtc = DateTime.UtcNow.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var req = new ScheduleBundleRequest
        {
          Type = "schedule",
          Date = whenUtc,
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
                          Content = "NYX smoke test ✅",
                          Image = new()
                          {
                              new UploadRefDto { Id = upload.Id!, Path = upload.Path! }
                          }
                      }
                  },
                  Settings = new YouTubeSettings()
                  { 
                    Title = "Nyx Smoke Test"
                  }
              }
          }
        };

      var json = JsonConvert.SerializeObject(req, Formatting.Indented);
      _logger.LogInformation("Schedule payload:\n{Json}", json);

      var scheduleResult = await _postizEngine.ScheduleBundleAsync(req, stoppingToken);

        _logger.LogInformation("✅ Smoke scheduled: {Result}", scheduleResult);

        Console.WriteLine($"[{DateTime.Now.ToString("g")}] {nameof(VideoPipelineWorker)} has ran an execution cycle!");
        //await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
      //} while(!stoppingToken.IsCancellationRequested);
    }
  }
}
