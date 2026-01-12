using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models
{
  public sealed class ScheduleBundleRequest
  {
    [JsonProperty("type")]
    public string Type { get; set; } = "schedule";

    [JsonProperty("date")]
    public string Date { get; set; } = "";

    [JsonProperty("shortLink")]
    public bool ShortLink { get; set; } = false;

    [JsonProperty("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonProperty("posts")]
    public List<PostItemDto> Posts { get; set; } = new();
  }
}
