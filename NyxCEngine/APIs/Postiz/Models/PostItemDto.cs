using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models
{
  public sealed class PostItemDto
  {
    [JsonProperty("integration")]
    public IntegrationRefWithIdentifier Integration { get; set; } = new();

    [JsonProperty("value")]
    public List<PostValueDto> Value { get; set; } = new();

    [JsonProperty("settings")]
    public object Settings { get; set; } = new();
  }

  public sealed class PostValueDto
  {
    [JsonProperty("content")]
    public string Content { get; set; } = "";

    [JsonProperty("image")]
    public List<UploadRefDto> Image { get; set; } = new();
  }

  public sealed class UploadRefDto
  {
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("path")]
    public string Path { get; set; } = "";
  }

  public sealed class IntegrationRefWithIdentifier
  {
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("identifier")]
    public string? Identifier { get; set; }
  }
}