using Microsoft.Extensions.Configuration;
using NyxCEngine.APIs.ElevenLabs.Models;
using NyxCEngine.Util;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace NyxCEngine.APIs.ElevenLabs
{
  internal sealed class ElevenLabsEngine : EngineBase
  {
    private readonly IConfiguration _config;

    public ElevenLabsEngine(IServiceProvider serviceProvider, IConfiguration config)
      : base(Program.ElevenLabsClientName, serviceProvider)
    {
      _config = config;
    }

    public async Task<byte[]> TextToSpeechPcmAsync(
      string text,
      CancellationToken ct,
      string? voiceIdOverride = null,
      string? modelIdOverride = null,
      string? outputFormatOverride = null,
      ElevenLabsVoiceSettings? voiceSettingsOverride = null)
    {
      if (string.IsNullOrWhiteSpace(text))
        throw new ArgumentException("Text is required.", nameof(text));

      var voiceId = voiceIdOverride ?? _config[EnvironmentVariableKeys.ElevenLabsVoiceId];
      if (string.IsNullOrWhiteSpace(voiceId))
        throw new InvalidOperationException($"Missing env var: {EnvironmentVariableKeys.ElevenLabsVoiceId}");

      var modelId = modelIdOverride ?? _config[EnvironmentVariableKeys.ElevenLabsModelId];
      var outputFormat = outputFormatOverride
        ?? _config[EnvironmentVariableKeys.ElevenLabsOutputFormat]
        ?? "pcm_24000";

      // POST /v1/text-to-speech/{voice_id}?output_format=pcm_24000
      var url = $"/v1/text-to-speech/{voiceId}?output_format={Uri.EscapeDataString(outputFormat)}";

      using var req = new HttpRequestMessage(HttpMethod.Post, url);

      // Accept raw audio bytes
      req.Headers.Accept.Clear();
      req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

      var body = new ElevenLabsTtsRequest
      {
        Text = text,
        ModelId = string.IsNullOrWhiteSpace(modelId) ? null : modelId,
        VoiceSettings = voiceSettingsOverride
      };

      var json = JsonConvert.SerializeObject(body, Formatting.None);
      req.Content = new StringContent(json, Encoding.UTF8, "application/json");

      using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
      if (!resp.IsSuccessStatusCode)
      {
        var err = await resp.Content.ReadAsStringAsync(ct);
        throw new Exception($"ElevenLabs TTS failed ({(int)resp.StatusCode}): {err}");
      }

      return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<(byte[] audioBytes, ElevenLabsAlignment alignment, string outputFormat)> TextToSpeechWithTimestampsPcmAsync(
      string text,
      CancellationToken ct,
      string? voiceIdOverride = null,
      string? modelIdOverride = null,
      string? outputFormatOverride = null,
      ElevenLabsVoiceSettings? voiceSettingsOverride = null)
    {
      if (string.IsNullOrWhiteSpace(text))
        throw new ArgumentException("Text is required.", nameof(text));

      var voiceId = voiceIdOverride ?? _config[EnvironmentVariableKeys.ElevenLabsVoiceId];
      if (string.IsNullOrWhiteSpace(voiceId))
        throw new InvalidOperationException($"Missing env var: {EnvironmentVariableKeys.ElevenLabsVoiceId}");

      var modelId = modelIdOverride
        ?? _config[EnvironmentVariableKeys.ElevenLabsModelId]
        ?? "eleven_multilingual_v2";

      var outputFormat = outputFormatOverride
        ?? _config[EnvironmentVariableKeys.ElevenLabsOutputFormat]
        ?? "pcm_24000";

      var url = $"/v1/text-to-speech/{voiceId}/with-timestamps?output_format={Uri.EscapeDataString(outputFormat)}";

      using var req = new HttpRequestMessage(HttpMethod.Post, url);

      var body = new ElevenLabsTtsRequest
      {
        Text = text,
        ModelId = string.IsNullOrWhiteSpace(modelId) ? null : modelId,
        VoiceSettings = voiceSettingsOverride
      };

      req.Content = new StringContent(JsonConvert.SerializeObject(body, Formatting.None), Encoding.UTF8, "application/json");

      using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
      if (!resp.IsSuccessStatusCode)
      {
        var err = await resp.Content.ReadAsStringAsync(ct);
        throw new Exception($"ElevenLabs TTS-with-timestamps failed ({(int)resp.StatusCode}): {err}");
      }

      var json = await resp.Content.ReadAsStringAsync(ct);
      var parsed = JsonConvert.DeserializeObject<ElevenLabsTtsWithTimestampsResponse>(json)
                   ?? throw new Exception("ElevenLabs response deserialized to null.");

      var audioBytes = Convert.FromBase64String(parsed.AudioBase64);

      // Python prefers normalized_alignment if present :contentReference[oaicite:5]{index=5}
      var alignment = parsed.NormalizedAlignment ?? parsed.Alignment;
      if (alignment is null || alignment.Characters.Count == 0)
        throw new Exception("ElevenLabs returned no alignment data.");

      return (audioBytes, alignment, outputFormat);
    }

  }
}