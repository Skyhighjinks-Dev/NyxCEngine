using Newtonsoft.Json;

namespace NyxCEngine.APIs.ElevenLabs.Models
{
  public sealed class ElevenLabsTtsWithTimestampsResponse
  {
    [JsonProperty("audio_base64")]
    public string AudioBase64 { get; set; } = null!;

    [JsonProperty("alignment")]
    public ElevenLabsAlignment? Alignment { get; set; }

    [JsonProperty("normalized_alignment")]
    public ElevenLabsAlignment? NormalizedAlignment { get; set; }
  }
}
