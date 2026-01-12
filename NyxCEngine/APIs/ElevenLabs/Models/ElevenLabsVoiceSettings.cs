using Newtonsoft.Json;

namespace NyxCEngine.APIs.ElevenLabs.Models
{
  public sealed class ElevenLabsVoiceSettings
  {
    [JsonProperty("stability")]
    public double? Stability { get; set; }

    [JsonProperty("similarity_boost")]
    public double? SimilarityBoost { get; set; }

    [JsonProperty("style")]
    public double? Style { get; set; }

    [JsonProperty("use_speaker_boost")]
    public bool? UseSpeakerBoost { get; set; }
  }
}
