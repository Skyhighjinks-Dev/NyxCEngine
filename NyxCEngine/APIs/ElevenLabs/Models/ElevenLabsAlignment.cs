using Newtonsoft.Json;

namespace NyxCEngine.APIs.ElevenLabs.Models
{
  public sealed class ElevenLabsAlignment
  {
    [JsonProperty("characters")]
    public List<string> Characters { get; set; } = new();

    [JsonProperty("character_start_times_seconds")]
    public List<double> CharacterStartTimesSeconds { get; set; } = new();

    [JsonProperty("character_end_times_seconds")]
    public List<double> CharacterEndTimesSeconds { get; set; } = new();
  }
}
