using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace NeteaseMusicCloudMatch.Core.Security;

public sealed record ParsedCookie(string HeaderValue, IReadOnlyDictionary<string, string> Values);

public static partial class CookieParser
{
    public static ParsedCookie Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new FormatException("Cookie 不能为空。");
        }

        if (input.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new FormatException("Cookie 包含不允许的控制字符。");
        }

        var value = input.Trim();
        if (value.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["Cookie:".Length..].Trim();
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalizedPairs = new List<string>();

        foreach (var rawPair in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = rawPair.IndexOf('=');
            if (separator <= 0)
            {
                throw new FormatException("Cookie 格式无效，应为 name=value; name2=value2。");
            }

            var name = rawPair[..separator].Trim();
            var cookieValue = rawPair[(separator + 1)..].Trim();
            if (!CookieNameRegex().IsMatch(name))
            {
                throw new FormatException("Cookie 包含无效的名称。");
            }

            values[name] = cookieValue;
            normalizedPairs.Add($"{name}={cookieValue}");
        }

        if (normalizedPairs.Count == 0)
        {
            throw new FormatException("Cookie 中没有可用的 name=value 项。");
        }

        return new ParsedCookie(
            string.Join("; ", normalizedPairs),
            new ReadOnlyDictionary<string, string>(values));
    }

    [GeneratedRegex("^[!#$%&'*+\\-.^_`|~0-9A-Za-z]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CookieNameRegex();
}
