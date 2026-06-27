using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed class Vo2MaxConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDouble();
        }

        var raw = reader.GetString() ?? throw new JsonException("vo2Max value was null.");
        var parts = raw.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 2)
        {
            var low = double.Parse(parts[0], CultureInfo.InvariantCulture);
            var high = double.Parse(parts[1], CultureInfo.InvariantCulture);
            return (low + high) / 2;
        }

        return double.Parse(raw, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}
