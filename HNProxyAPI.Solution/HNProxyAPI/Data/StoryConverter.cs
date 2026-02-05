using System.Text.Json;
using System.Text.Json.Serialization;

namespace HNProxyAPI.Data
{
    public class StoryConverter : JsonConverter<Story>
    {
        public override Story Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            int id = 0;
            string title = string.Empty;
            string uri = string.Empty;
            string postedBy = string.Empty;
            DateTime time = default;
            int score = 0;

            while (reader.Read()) 
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType ==  JsonTokenType.PropertyName)
                {
                    var propName = reader.GetString();
                    reader.Read();

                    switch (propName)
                    {
                        case "id":
                            if (reader.TokenType == JsonTokenType.Number)
                                id = reader.GetInt32();
                            break;
                        case "title":
                            title = reader.GetString() ?? "";
                            break;
                        case "url":
                            uri = reader.GetString() ?? "";
                            break;
                        case "by":
                            postedBy = reader.GetString() ?? "";
                            break;
                        case "time":
                            if (reader.TokenType == JsonTokenType.Number)
                            {
                                long unixTime = reader.GetInt64();
                                time = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
                            }
                            else if (reader.TokenType == JsonTokenType.String && long.TryParse(reader.GetString(), out long t))
                            {
                                time = DateTimeOffset.FromUnixTimeSeconds(t).UtcDateTime;
                            }
                            break;
                        case "score":
                            if (reader.TokenType == JsonTokenType.Number)
                                score = reader.GetInt32();
                            break;
                        default:
                            reader.Skip(); // ignores properties not used inside Story
                            break;
                    }
                }                
            }
            return new Story(id, title, uri, postedBy, time, score);
        }

        public override void Write(Utf8JsonWriter writer, Story value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", value.id);
            writer.WriteString("title", value.title);
            writer.WriteString("uri", value.uri);
            writer.WriteString("postedBy", value.postedBy);
            writer.WriteString("time", value.time);
            writer.WriteNumber("score", value.score);
            writer.WriteEndObject();
        }
    }
}
