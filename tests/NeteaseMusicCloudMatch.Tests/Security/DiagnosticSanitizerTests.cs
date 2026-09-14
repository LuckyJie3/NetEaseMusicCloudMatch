using NeteaseMusicCloudMatch.Core.Security;
using System.Text.Json;

namespace NeteaseMusicCloudMatch.Tests.Security;

public sealed class DiagnosticSanitizerTests
{
    [Theory]
    [InlineData("Cookie: MUSIC_U=one; __csrf=two")]
    [InlineData("Set-Cookie: MUSIC_A=three")]
    [InlineData("Authorization: Bearer four")]
    [InlineData("MUSIC_U=five; other=safe")]
    [InlineData("__csrf=six MUSIC_R_T=seven MUSIC_A=eight")]
    [InlineData("{\"Cookie\":\"MUSIC_U=nine\"}")]
    [InlineData("Cookie=ten; harmless=value")]
    public void Redact_RemovesSensitiveValues(string input)
    {
        var result = DiagnosticSanitizer.Redact(input);

        Assert.Contains(DiagnosticSanitizer.Redacted, result);
        foreach (var secret in new[] { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" })
        {
            Assert.DoesNotContain(secret, result, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SanitizeJson_RedactsCredentialPropertiesAndEmbeddedCookieValues()
    {
        const string input = """
            {"code":200,"Cookie":"session-secret","nested":{"MUSIC_U":"music-secret","message":"__csrf=csrf-secret"}}
            """;

        var result = DiagnosticSanitizer.SanitizeJson(input);

        using var document = JsonDocument.Parse(result);
        Assert.Equal(200, document.RootElement.GetProperty("code").GetInt32());
        Assert.DoesNotContain("session-secret", result, StringComparison.Ordinal);
        Assert.DoesNotContain("music-secret", result, StringComparison.Ordinal);
        Assert.DoesNotContain("csrf-secret", result, StringComparison.Ordinal);
        Assert.Contains(DiagnosticSanitizer.Redacted, result, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeJson_TruncatesLargeArraysButKeepsValidJson()
    {
        var input = JsonSerializer.Serialize(Enumerable.Range(1, 30));

        var result = DiagnosticSanitizer.SanitizeJson(input);

        using var document = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(13, document.RootElement.GetArrayLength());
        Assert.Contains("其余 18 项已省略", result, StringComparison.Ordinal);
    }
}
