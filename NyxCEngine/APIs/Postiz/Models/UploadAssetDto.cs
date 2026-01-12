using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models
{
  public sealed class UploadAssetDto
  {
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("path")]
    public string? Path { get; set; }

    public UploadAssetRawDto Raw { get; set; } = new();
  }
}
