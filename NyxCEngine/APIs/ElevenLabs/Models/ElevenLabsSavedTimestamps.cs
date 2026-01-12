using Newtonsoft.Json;

namespace NyxCEngine.APIs.ElevenLabs.Models
{
  public sealed class ElevenLabsSavedTimestamps
  {
    [JsonProperty("alignment")]
    public ElevenLabsAlignment Alignment { get; set; } = null!;
  }
}
