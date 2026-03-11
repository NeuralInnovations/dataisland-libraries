using System.Text.Json;
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
        ReadCommentHandling = JsonCommentHandling.Skip
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
}
