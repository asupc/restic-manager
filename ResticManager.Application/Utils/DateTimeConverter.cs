using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResticManager.Application.Utils;

public class UtcToLocalDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDateTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // 将UTC时间转换为本地时间
        var localTime = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        writer.WriteStringValue(localTime);
    }
}

public class UtcToLocalNullableDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
            
        return reader.GetDateTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            // 将UTC时间转换为本地时间
            var localTime = value.Value.Kind == DateTimeKind.Utc ? value.Value.ToLocalTime() : value.Value;
            writer.WriteStringValue(localTime);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}