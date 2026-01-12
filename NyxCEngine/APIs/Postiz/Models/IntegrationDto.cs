using Newtonsoft.Json;

namespace NyxCEngine.APIs.Postiz.Models
{
  public sealed class IntegrationDto
  {
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("identifier")]
    public string? Identifier { get; set; }

    [JsonProperty("profile")]
    public string? Profile { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("disabled")]
    public bool Disabled { get; set; }

    [JsonProperty("picture")]
    public string? Picture { get; set; }

    [JsonProperty("customer")]
    public CustomerDto Customer { get; set; } = new();
  }
}
