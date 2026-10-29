using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingAssistant.API;

/// <summary>
/// Convertidor JSON que garantiza que todos los DateTime se serialicen con sufijo 'Z' (UTC)
/// para que el navegador los interprete como hora universal y los convierta automáticamente
/// a la zona horaria local del usuario.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        if (DateTime.TryParse(str, out var dt))
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }
        return DateTime.UtcNow;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Siempre serializar como UTC con sufijo 'Z'
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
    }
}
