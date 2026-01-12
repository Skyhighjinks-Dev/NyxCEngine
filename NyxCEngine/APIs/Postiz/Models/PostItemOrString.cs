using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NyxCEngine.APIs.Postiz.Models
{
  [JsonConverter(typeof(PostItemOrStringConverter))]
  public sealed class PostItemOrString
  {
    public PostItemDto? Item { get; set; }
    public string? RawString { get; set; }

    public bool IsObject => Item != null;
    public bool IsString => RawString != null;
  }

  public sealed class PostItemOrStringConverter : JsonConverter<PostItemOrString>
  {
    public override PostItemOrString ReadJson(JsonReader reader, Type objectType, PostItemOrString? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
      if (reader.TokenType == JsonToken.String)
      {
        return new PostItemOrString { RawString = reader.Value?.ToString() };
      }

      if (reader.TokenType == JsonToken.StartObject)
      {
        var obj = JObject.Load(reader);
        return new PostItemOrString
        {
          Item = obj.ToObject<PostItemDto>(serializer)
        };
      }

      var token = JToken.Load(reader);
      return new PostItemOrString { RawString = token.ToString() };
    }

    public override void WriteJson(JsonWriter writer, PostItemOrString? value, JsonSerializer serializer)
    {
      if (value?.Item != null)
        serializer.Serialize(writer, value.Item);
      else
        writer.WriteValue(value?.RawString);
    }
  }
}
