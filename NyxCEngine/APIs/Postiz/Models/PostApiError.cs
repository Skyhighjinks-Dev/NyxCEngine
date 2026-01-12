using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models
{
  public sealed class PostizApiError
  {
    [JsonProperty("statusCode")]
    public int? StatusCode { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }

    public override string ToString()
        => $"StatusCode={StatusCode}, Error={Error}, Message={Message}";
  }
}