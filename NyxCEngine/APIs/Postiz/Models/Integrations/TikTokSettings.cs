using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models.Integrations
{
  internal sealed class TikTokSettings
  {
    [JsonProperty("__type")]
    public string TypeMarker { get; set; } = "tiktok";

    [JsonProperty("title")]
    public string Title { get; set; } = "";

    [JsonProperty("privacy_level")]
    public string PrivacyLevel { get; set; } = "PUBLIC_TO_EVERYONE";

    [JsonProperty("duet")]
    public bool Duet { get; set; } = true;

    [JsonProperty("stitch")]
    public bool Stitch { get; set; } = true;

    [JsonProperty("comment")]
    public bool Comment { get; set; } = true;

    [JsonProperty("autoAddMusic")]
    public string AutoAddMusic { get; set; } = "no";

    [JsonProperty("brand_content_toggle")]
    public bool BrandContentToggle { get; set; } = false;

    [JsonProperty("brand_organic_toggle")]
    public bool BrandOrganicToggle { get; set; } = false;

    [JsonProperty("video_made_with_ai")]
    public bool VideoMadeWithAi { get; set; } = false;

    [JsonProperty("content_posting_method")]
    public string ContentPostingMethod { get; set; } = "DIRECT_POST";
  }
}
