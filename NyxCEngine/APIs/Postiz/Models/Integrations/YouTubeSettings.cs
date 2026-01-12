using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models.Integrations
{
  internal sealed class YouTubeSettings
  {
    [JsonProperty("__type")]
    public string TypeMarker { get; set; } = "youtube";

    [JsonProperty("title")]
    public string Title { get; set; } = "";

    [JsonProperty("type")]
    public string Visibility { get; set; } = "public"; // public/unlisted/private

    [JsonProperty("selfDeclaredMadeForKids")]
    public string MadeForKids { get; set; } = "no";

    [JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)]
    public List<TagDto> Tags { get; set; } = null!;
  }
}
