using NeteaseMusicCloudMatch.App.Services;
using Serilog;

namespace NeteaseMusicCloudMatch.Tests.App;

public sealed class ApplicationLogReaderTests
{
    [Fact]
    public void Parse_SeparatesAndPrettyPrintsResponseJson()
    {
        const string line = "2026-09-14 20:15:00.000 +08:00 [INF] Netease request completed. Operation=GetSongDetail Method=GET Host=music.163.com Endpoint=/api/song/detail/ HttpStatus=200 ApiCode=200 ElapsedMs=45 CorrelationId=abc123 ResponseJson={\"code\":200,\"songs\":[{\"id\":186137}]}";

        var entry = ApplicationLogReader.Parse(line);

        Assert.NotNull(entry);
        Assert.Equal("查询目标歌曲", entry.Title);
        Assert.Contains("GET /api/song/detail/", entry.TechnicalDetails, StringComparison.Ordinal);
        Assert.DoesNotContain("ResponseJson", entry.TechnicalDetails, StringComparison.Ordinal);
        Assert.True(entry.HasResponseJson);
        Assert.Contains(Environment.NewLine, entry.ResponseJson, StringComparison.Ordinal);
        Assert.Contains("\"id\": 186137", entry.ResponseJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClearAsync_TruncatesLocalLogFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"NeteaseMusicCloudMatch.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var first = Path.Combine(directory, "app-1.log");
        var second = Path.Combine(directory, "app-2.log");
        await File.WriteAllTextAsync(first, "first");
        await File.WriteAllTextAsync(second, "second");

        try
        {
            var reader = new ApplicationLogReader(directory);

            await reader.ClearAsync();

            Assert.Equal(0, new FileInfo(first).Length);
            Assert.Equal(0, new FileInfo(second).Length);
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task ClearAsync_WorksWhileSharedSerilogSinkIsOpenAndFutureWritesContinue()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"NeteaseMusicCloudMatch.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "app.log");

        try
        {
            using (var logger = new LoggerConfiguration().WriteTo.File(file, shared: true).CreateLogger())
            {
                logger.Information("before-clear");
                var reader = new ApplicationLogReader(directory);

                await reader.ClearAsync();
                logger.Information("after-clear");
            }

            var content = await File.ReadAllTextAsync(file);
            Assert.DoesNotContain("before-clear", content, StringComparison.Ordinal);
            Assert.Contains("after-clear", content, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
