using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models
{
  public sealed class ListPostsResponse
  {
    [JsonProperty("data")]
    public List<PostItemOrString> Data { get; set; } = [];
  }
}
