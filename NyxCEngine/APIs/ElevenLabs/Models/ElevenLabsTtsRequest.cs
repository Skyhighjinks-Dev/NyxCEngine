using Newtonsoft.Json;

namespace NyxCEngine.APIs.ElevenLabs.Models
{
  public sealed class ElevenLabsTtsRequest
  {
    [JsonProperty("text")]
    public string Text { get; set; } = null!;

    [JsonProperty("model_id")]
    public string? ModelId { get; set; }

    [JsonProperty("voice_settings")]
    public ElevenLabsVoiceSettings? VoiceSettings { get; set; }
  }
}
