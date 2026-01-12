using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models
{
  internal class TagDto
  {
    [JsonProperty("value")]
    public string Value { get; set; }

    [JsonProperty("label")]
    public string Label { get; set; }
  }
}
