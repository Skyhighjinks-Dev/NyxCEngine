using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models
{
  internal sealed class PostDto
  {
    [JsonProperty("id")]
    public object? Id { get; set; }

    [JsonProperty("state")]
    public string? State { get; set; }

    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonProperty("releaseUrl")]
    public string? ReleaseUrl { get; set; }

    [JsonProperty("integration")]
    public PostIntegrationDto? Integration { get; set; }

    public string? IdString => Id?.ToString();
  }

  internal sealed class PostIntegrationDto
  {
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("identifier")]
    public string? Identifier { get; set; }
  }
}
