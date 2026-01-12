using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models
{
  public class UploadAssetRawDto
  {
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("path")]
    public string? Path { get; set; }

    [JsonProperty("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonProperty("alt")]
    public string? Alt { get; set; }
  }
}
