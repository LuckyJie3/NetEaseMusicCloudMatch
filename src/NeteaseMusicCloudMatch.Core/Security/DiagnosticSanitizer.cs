using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NeteaseMusicCloudMatch.Core.Security;

public static partial class DiagnosticSanitizer
{
    public const string Redacted = "[REDACTED]";

    private const int MaximumArrayItems = 12;
    private const int MaximumObjectProperties = 64;
    private const int MaximumDepth = 8;
    private const int MaximumStringLength = 512;
    private const int MaximumNodes = 800;

    private static readonly HashSet<string> SensitiveJsonProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cookie",
        "Set-Cookie",
        "Authorization",
        "MUSIC_U",
        "__csrf",
        "MUSIC_A",
        "MUSIC_R_T"
    };

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var result = JsonSecretRegex().Replace(
            value,
            match => $"\"{match.Groups["name"].Value}\":\"{Redacted}\"");
        result = HeaderSecretRegex().Replace(
            result,
            match => $"{match.Groups["name"].Value}: {Redacted}");
        result = NamedCookieRegex().Replace(
            result,
            match => $"{match.Groups["name"].Value}={Redacted}");
        return result;
    }

    /// <summary>
    /// Produces a compact, valid and bounded JSON value for local diagnostics.
    /// Credential-like properties are replaced before the result can reach a logger.
    /// </summary>
    public static string SanitizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(
                       stream,
                       new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            {
                var budget = new JsonWriteBudget(MaximumNodes);
                WriteSanitizedElement(writer, document.RootElement, 0, budget);
            }

            return Redact(Encoding.UTF8.GetString(stream.ToArray()));
        }
        catch (JsonException)
        {
            var bounded = json.Length <= 2048 ? json : string.Concat(json.AsSpan(0, 2048), "…");
            return Redact(bounded);
        }
    }

    private static void WriteSanitizedElement(
        Utf8JsonWriter writer,
        JsonElement element,
        int depth,
        JsonWriteBudget budget)
    {
        if (depth >= MaximumDepth || !budget.TryConsume())
        {
            writer.WriteStringValue("[TRUNCATED]");
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var propertyCount = 0;
                foreach (var property in element.EnumerateObject())
                {
                    if (propertyCount++ >= MaximumObjectProperties)
                    {
                        writer.WriteString("_diagnosticNotice", "其余字段已省略");
                        break;
                    }

                    writer.WritePropertyName(property.Name);
                    if (SensitiveJsonProperties.Contains(property.Name))
                    {
                        writer.WriteStringValue(Redacted);
                    }
                    else
                    {
                        WriteSanitizedElement(writer, property.Value, depth + 1, budget);
                    }
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                var itemCount = 0;
                var totalItems = element.GetArrayLength();
                foreach (var item in element.EnumerateArray())
                {
                    if (itemCount++ >= MaximumArrayItems)
                    {
                        writer.WriteStringValue($"… 其余 {totalItems - MaximumArrayItems} 项已省略");
                        break;
                    }

                    WriteSanitizedElement(writer, item, depth + 1, budget);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                var value = element.GetString() ?? string.Empty;
                writer.WriteStringValue(
                    value.Length <= MaximumStringLength
                        ? value
                        : string.Concat(value.AsSpan(0, MaximumStringLength), "…"));
                break;

            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), true);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            default:
                writer.WriteNullValue();
                break;
        }
    }

    private sealed class JsonWriteBudget(int remainingNodes)
    {
        private int _remainingNodes = remainingNodes;

        public bool TryConsume() => _remainingNodes-- > 0;
    }

    [GeneratedRegex("\"(?<name>Cookie|Set-Cookie|Authorization|MUSIC_U|__csrf|MUSIC_A|MUSIC_R_T)\"\\s*:\\s*\"[^\"]*\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonSecretRegex();

    [GeneratedRegex("(?im)(?<name>Cookie|Set-Cookie|Authorization)\\s*:\\s*[^\\r\\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderSecretRegex();

    [GeneratedRegex("(?i)(?<name>Cookie|Set-Cookie|Authorization|MUSIC_U|__csrf|MUSIC_A|MUSIC_R_T)\\s*=\\s*[^;\\s,}\"]*", RegexOptions.CultureInvariant)]
    private static partial Regex NamedCookieRegex();
}
