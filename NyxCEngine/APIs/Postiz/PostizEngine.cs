using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NyxCEngine.APIs.Postiz.Models;
using System.Net.Http.Headers;
using System.Text;

namespace NyxCEngine.APIs.Postiz
{
  internal class PostizEngine : EngineBase
  {
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
      NullValueHandling = NullValueHandling.Ignore,
      MissingMemberHandling = MissingMemberHandling.Ignore
    };

    private readonly ILogger<PostizEngine> _logger;

    public PostizEngine(IServiceProvider serviceProvider, ILogger<PostizEngine> logger)
      : base(Program.PostizClientName, serviceProvider)
    {
      _logger = logger;
    }

    public async Task<ListIntegrationsResponse> ListIntegrationsAsync(CancellationToken ct = default)
    {
      using var req = new HttpRequestMessage(HttpMethod.Get, "integrations");
      using var res = await _httpClient.SendAsync(req, ct);
      await EnsureSuccessOrThrow(res);

      var json = await res.Content.ReadAsStringAsync(ct);
      return JsonConvert.DeserializeObject<ListIntegrationsResponse>(json, JsonSettings) ?? new();
    }

    public async Task<ListPostsResponse> ListPostsAsync(
      string startDateIso,
      string endDateIso,
      string? customerId = null,
      CancellationToken ct = default)
    {
      var qs = new List<string>
      {
        $"startDate={Uri.EscapeDataString(startDateIso)}",
        $"endDate={Uri.EscapeDataString(endDateIso)}"
      };
      if (!string.IsNullOrWhiteSpace(customerId))
        qs.Add($"customer={Uri.EscapeDataString(customerId)}");

      using var req = new HttpRequestMessage(HttpMethod.Get, $"posts?{string.Join("&", qs)}");
      using var res = await _httpClient.SendAsync(req, ct);
      await EnsureSuccessOrThrow(res);

      var json = await res.Content.ReadAsStringAsync(ct);

      // Sometimes API returns list, sometimes wrapper; if you discover wrapper later, adapt here.
      return JsonConvert.DeserializeObject<ListPostsResponse>(json, JsonSettings) ?? new();
    }

    public async Task<UploadAssetDto> UploadMediaAsync(string filePath, CancellationToken ct = default)
    {
      if (!File.Exists(filePath))
        throw new FileNotFoundException("Upload file not found.", filePath);

      await using var fs = File.OpenRead(filePath);

      using var form = new MultipartFormDataContent();
      var fileContent = new StreamContent(fs);

      var contentType = GuessContentType(filePath);
      fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

      form.Add(fileContent, "file", Path.GetFileName(filePath));

      using var req = new HttpRequestMessage(HttpMethod.Post, "upload")
      {
        Content = form
      };

      using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
      await EnsureSuccessOrThrow(res);

      var json = await res.Content.ReadAsStringAsync(ct);
      var raw = JsonConvert.DeserializeObject<UploadAssetRawDto>(json, JsonSettings)
                ?? throw new InvalidOperationException("Postiz upload returned empty/invalid JSON.");

      return new UploadAssetDto
      {
        Id = raw.Id ?? "",
        Path = raw.Path ?? "",
        Raw = raw
      };
    }

    public async Task<ScheduleBundleResponse> ScheduleBundleAsync(
      ScheduleBundleRequest payload,
      CancellationToken ct = default)
    {
      var jsonBody = JsonConvert.SerializeObject(payload, JsonSettings);
      using var req = new HttpRequestMessage(HttpMethod.Post, "posts")
      {
        Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
      };

      var jsonReq = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
      {
        NullValueHandling = NullValueHandling.Ignore
      });

      _logger.LogInformation("Schedule payload: {Json}", jsonReq);

      using var res = await _httpClient.SendAsync(req, ct);
      await EnsureSuccessOrThrow(res);

      var json = await res.Content.ReadAsStringAsync(ct);
      return JsonConvert.DeserializeObject<ScheduleBundleResponse>(json, JsonSettings)
             ?? new ScheduleBundleResponse();
    }

    private static async Task EnsureSuccessOrThrow(HttpResponseMessage res)
    {
      if (res.IsSuccessStatusCode) return;

      var body = "";
      try { body = await res.Content.ReadAsStringAsync(); } catch { /* ignore */ }

      throw new PostizHttpException(
        (int)res.StatusCode,
        $"Postiz request failed: {(int)res.StatusCode} {res.ReasonPhrase}",
        body);
    }

    private static string GuessContentType(string filePath)
    {
      var ext = Path.GetExtension(filePath).ToLowerInvariant();
      return ext switch
      {
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => "application/octet-stream"
      };
    }
  }
}