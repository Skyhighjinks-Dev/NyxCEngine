using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models.Integrations
{
  internal sealed class InstagramSettings
  {
    [JsonProperty("__type")]
    public string TypeMarker { get; set; } = "instagram";

    [JsonProperty("post_type")]
    public string PostType { get; set; } = "post"; // post/story

    [JsonProperty("collaborators")]
    public List<string> Collaborators { get; set; } = new();
  }
}
