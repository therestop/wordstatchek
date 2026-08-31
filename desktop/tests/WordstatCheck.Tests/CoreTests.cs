using System.Net;
using System.Text;
using ClosedXML.Excel;
using WordstatCheck.Core;
using Xunit;

namespace WordstatCheck.Tests;

public sealed class CoreTests
{
    [Fact]
    public async Task PhraseFile_RemovesBlanksAndDuplicates()
    {
        var root = NewTempDirectory();
        var path = Path.Combine(root, "input.txt");
        await File.WriteAllTextAsync(path, "\ufeffсварка\n\n сварка \nметалл\n");

        var phrases = await PhraseFile.ReadAsync(path);

        Assert.Equal(["сварка", "металл"], phrases);
    }

    [Fact]
    public async Task Checkpoint_RoundTrips()
    {
        var path = Path.Combine(NewTempDirectory(), "checkpoint.json");
        var store = new CheckpointStore(path);
        var source = new CheckResult("сварка", 42, "nonzero", "", DateTimeOffset.UtcNow);

        await store.SaveAsync([source]);
        var loaded = await store.LoadAsync();

        Assert.Equal(42, loaded["сварка"].TotalCount);
    }

    [Fact]
    public async Task Api_RetriesLimitAndUsesExpectedPayload()
    {
        var handler = new QueueHandler(
            Response(HttpStatusCode.TooManyRequests, "{\"message\":\"limit\"}"),
            Response(HttpStatusCode.OK, "{\"totalCount\":17}"));
        var options = new WordstatOptions
        {
            ApiKey = "secret",
            FolderId = "folder",
            Attempts = 2,
            BaseDelay = TimeSpan.Zero,
            MaxDelay = TimeSpan.Zero
        };
        var client = new WordstatApiClient(new HttpClient(handler), options);

        var count = await client.GetTotalCountAsync("фраза");

        Assert.Equal(17, count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("Api-Key", handler.Requests[0].Scheme);
        Assert.Contains("\"folderId\":\"folder\"", handler.Requests[0].Body);
    }

    [Fact]
    public async Task Api_MarksAuthorizationErrorAsFatal()
    {
        var handler = new QueueHandler(Response(HttpStatusCode.Forbidden, "{\"message\":\"denied\"}"));
        var options = new WordstatOptions { ApiKey = "bad", FolderId = "folder" };

        var error = await Assert.ThrowsAsync<WordstatApiException>(
            () => new WordstatApiClient(new HttpClient(handler), options).GetTotalCountAsync("фраза"));

        Assert.True(error.Fatal);
    }

    [Fact]
    public void Export_CreatesAllOutputsAndWorkbookSheets()
    {
        var root = NewTempDirectory();
        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<CheckResult> results =
        [
            new("one", 10, "nonzero", "", now),
            new("two", 0, "zero", "", now),
            new("three", null, "error", "boom", now)
        ];

        ExportService.Export(results, root);

        Assert.True(File.Exists(Path.Combine(root, "wordstat_all.csv")));
        Assert.True(File.Exists(Path.Combine(root, "wordstat_nonzero.txt")));
        Assert.True(File.Exists(Path.Combine(root, "wordstat_zero.txt")));
        Assert.True(File.Exists(Path.Combine(root, "wordstat_errors.txt")));
        using var workbook = new XLWorkbook(Path.Combine(root, "wordstat_results.xlsx"));
        Assert.Equal(["Все", "Ненулевые", "Нулевые", "Ошибки"], workbook.Worksheets.Select(x => x.Name));
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "wordstatchek-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> queue = new(responses);
        public List<(string? Scheme, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Add((request.Headers.Authorization?.Scheme, await request.Content!.ReadAsStringAsync(token)));
            return queue.Dequeue();
        }
    }
}
