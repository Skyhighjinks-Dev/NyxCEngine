using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models
{
  public sealed class ScheduleBundleResultItem
  {
    [JsonProperty("postId")]
    public string PostId { get; set; } = null!;

    [JsonProperty("integration")]
    public string Integration { get; set; } = null!;
  }
}
