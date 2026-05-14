using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Dataisland.LLM;

public static partial class JsonResponseParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new FlexibleLlmDateTimeConverter() }
    };

    public static T? TryParse<T>(string? content, ILogger logger) where T : class
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var json = ExtractJson(content);
        if (json is null)
        {
            logger.LogWarning("No JSON found in LLM response");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse LLM JSON response");
            return null;
        }
    }

    internal static string? ExtractJson(string content)
    {
        // Try to extract from markdown code block first
        var match = CodeBlockRegex().Match(content);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        // Find first { or [ to locate JSON start
        var objectStart = content.IndexOf('{');
        var arrayStart = content.IndexOf('[');

        if (objectStart < 0 && arrayStart < 0)
            return null;

        int start;
        char openChar;
        char closeChar;

        if (objectStart >= 0 && (arrayStart < 0 || objectStart < arrayStart))
        {
            start = objectStart;
            openChar = '{';
            closeChar = '}';
        }
        else
        {
            start = arrayStart;
            openChar = '[';
            closeChar = ']';
        }

        // Find matching closing bracket
        var depth = 0;
        var inString = false;
        var escape = false;

        for (var i = start; i < content.Length; i++)
        {
            var c = content[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString) continue;

            if (c == openChar) depth++;
            else if (c == closeChar) depth--;

            if (depth == 0)
                return content[start..(i + 1)];
        }

        // Fallback: return from start to end
        return content[start..];
    }

    [GeneratedRegex(@"```(?:json)?\s*\n?([\s\S]*?)\n?\s*```", RegexOptions.Compiled)]
    private static partial Regex CodeBlockRegex();

    private sealed class FlexibleLlmDateTimeConverter : JsonConverter<DateTime>
    {
        private static readonly string[] PreferredObjectKeys =
        [
            "value",
            "text",
            "dateTime",
            "datetime",
            "date_time",
            "iso",
            "isoDate",
            "timestamp",
            "raw",
            "date"
        ];

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => ParseDateTimeOrDefault(reader.GetString()),
                JsonTokenType.Number => TryReadUnixTimestamp(ref reader, out var value) ? value : DateTime.MinValue,
                JsonTokenType.StartObject => ReadObjectOrDefault(ref reader),
                JsonTokenType.Null => DateTime.MinValue,
                _ => throw new JsonException($"Expected date string or object, got {reader.TokenType}.")
            };
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);

        private static DateTime ReadObjectOrDefault(ref Utf8JsonReader reader)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            return TryReadDateTime(doc.RootElement, out var value) ? value : DateTime.MinValue;
        }

        private static bool TryReadDateTime(JsonElement element, out DateTime value)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return TryParseDateTimeString(element.GetString(), out value);
                case JsonValueKind.Number:
                    return TryReadUnixTimestamp(element, out value);
                case JsonValueKind.Object:
                    return TryReadDateTimeObject(element, out value);
                default:
                    value = DateTime.MinValue;
                    return false;
            }
        }

        private static bool TryReadDateTimeObject(JsonElement element, out DateTime value)
        {
            if (TryReadDateAndTimeFields(element, out value))
                return true;

            foreach (var key in PreferredObjectKeys)
            {
                if (TryGetProperty(element, key, out var property) && TryReadDateTime(property, out value))
                    return true;
            }

            if (TryReadDateParts(element, out value))
                return true;

            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String
                    && LooksLikeDateProperty(property.Name)
                    && TryParseDateTimeString(property.Value.GetString(), out value))
                    return true;
            }

            value = DateTime.MinValue;
            return false;
        }

        private static bool TryReadDateAndTimeFields(JsonElement element, out DateTime value)
        {
            if (!TryGetProperty(element, "date", out var dateProperty)
                || !TryGetProperty(element, "time", out var timeProperty)
                || timeProperty.ValueKind != JsonValueKind.String)
            {
                value = DateTime.MinValue;
                return false;
            }

            var timeText = timeProperty.GetString();
            if (dateProperty.ValueKind == JsonValueKind.String
                && TryParseDateTimeString($"{dateProperty.GetString()} {timeText}", out value))
                return true;

            if (TryReadDateTime(dateProperty, out var date)
                && TimeSpan.TryParse(timeText, CultureInfo.InvariantCulture, out var time))
            {
                value = date.Date.Add(time);
                return true;
            }

            value = DateTime.MinValue;
            return false;
        }

        private static bool TryReadDateParts(JsonElement element, out DateTime value)
        {
            if (!TryReadIntProperty(element, "year", out var year)
                || !TryReadIntProperty(element, "month", out var month)
                || !TryReadIntProperty(element, "day", out var day))
            {
                value = DateTime.MinValue;
                return false;
            }

            TryReadIntProperty(element, "hour", out var hour);
            TryReadIntProperty(element, "minute", out var minute);
            TryReadIntProperty(element, "second", out var second);
            TryReadIntProperty(element, "millisecond", out var millisecond);

            try
            {
                value = new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Unspecified);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                value = DateTime.MinValue;
                return false;
            }
        }

        private static bool TryParseDateTimeString(string? raw, out DateTime value)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                value = DateTime.MinValue;
                return false;
            }

            var text = raw.Trim();
            return DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                out value)
                   || DateTime.TryParse(
                       text,
                       CultureInfo.CurrentCulture,
                       DateTimeStyles.AllowWhiteSpaces,
                       out value);
        }

        private static DateTime ParseDateTimeOrDefault(string? raw) =>
            TryParseDateTimeString(raw, out var value) ? value : DateTime.MinValue;

        private static bool TryReadUnixTimestamp(ref Utf8JsonReader reader, out DateTime value)
        {
            if (reader.TryGetInt64(out var timestamp))
                return TryConvertUnixTimestamp(timestamp, out value);

            value = DateTime.MinValue;
            return false;
        }

        private static bool TryReadUnixTimestamp(JsonElement element, out DateTime value)
        {
            if (element.TryGetInt64(out var timestamp))
                return TryConvertUnixTimestamp(timestamp, out value);

            value = DateTime.MinValue;
            return false;
        }

        private static bool TryConvertUnixTimestamp(long timestamp, out DateTime value)
        {
            try
            {
                value = timestamp > 100_000_000_000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime
                    : DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                value = DateTime.MinValue;
                return false;
            }
        }

        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static bool TryReadIntProperty(JsonElement element, string name, out int value)
        {
            if (TryGetProperty(element, name, out var property))
            {
                if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
                    return true;

                if (property.ValueKind == JsonValueKind.String
                    && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    return true;
            }

            value = 0;
            return false;
        }

        private static bool LooksLikeDateProperty(string name) =>
            name.Contains("date", StringComparison.OrdinalIgnoreCase)
            || name.Contains("time", StringComparison.OrdinalIgnoreCase);
    }
}
