using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace NyxCEngine.APIs
{
  internal static class HttpJsonExtensions
  {
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
      NullValueHandling = NullValueHandling.Ignore,
      MissingMemberHandling = MissingMemberHandling.Ignore
    };

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage resp, CancellationToken ct = default)
    {
      var body = await resp.Content.ReadAsStringAsync(ct);
      try
      {
        return JsonConvert.DeserializeObject<T>(body, JsonSettings)
               ?? throw new InvalidOperationException($"Failed to deserialize JSON into {typeof(T).Name}. Body: {body}");
      }
      catch (JsonException ex)
      {
        throw new InvalidOperationException($"Invalid JSON for {typeof(T).Name}. Body: {body}", ex);
      }
    }

    public static StringContent ToJsonContent(this object obj)
    // Postiz is JSON API
      => new StringContent(JsonConvert.SerializeObject(obj, JsonSettings), Encoding.UTF8, "application/json");

    public static async Task EnsureSuccessWithBodyAsync(this HttpResponseMessage resp, CancellationToken ct = default)
    {
      if (resp.IsSuccessStatusCode) return;

      var body = await resp.Content.ReadAsStringAsync(ct);
      throw new HttpRequestException(
        $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}",
        null,
        resp.StatusCode
      );
    }

    public static MultipartFormDataContent CreateMultipart(string fieldName, Stream stream, string fileName, string contentType)
    {
      var multi = new MultipartFormDataContent();
      var file = new StreamContent(stream);
      file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
      multi.Add(file, fieldName, fileName);
      return multi;
    }
  }
}
