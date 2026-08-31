namespace WordstatCheck.Core;

public sealed class WordstatRunner(
    WordstatApiClient client,
    CheckpointStore checkpoint,
    JsonLineLogger logger)
{
    public async Task<IReadOnlyList<CheckResult>> RunAsync(
        IReadOnlyList<string> phrases,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken token = default)
    {
        var allowed = phrases.ToHashSet(StringComparer.Ordinal);
        var results = (await checkpoint.LoadAsync(token))
            .Where(pair => allowed.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        await logger.WriteAsync("run_started", new { total = phrases.Count, resumed = results.Count }, token);
        progress?.Report(new ProgressUpdate(Summarize(phrases.Count, results.Values), null));

        foreach (var phrase in phrases)
        {
            if (results.ContainsKey(phrase)) continue;
            if (token.IsCancellationRequested) break;

            CheckResult result;
            try
            {
                var count = await client.GetTotalCountAsync(phrase, token);
                result = new CheckResult(
                    phrase,
                    count,
                    count > 0 ? "nonzero" : "zero",
                    "",
                    DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (WordstatApiException error) when (!error.Fatal)
            {
                result = new CheckResult(phrase, null, "error", error.Message, DateTimeOffset.UtcNow);
            }

            results[phrase] = result;
            // Уже полученный ответ нужно записать даже при одновременной отмене.
            await checkpoint.SaveAsync(results.Values, CancellationToken.None);
            await logger.WriteAsync("phrase_checked", new
            {
                phrase,
                status = result.Status,
                total_count = result.TotalCount,
                error = result.Error
            }, CancellationToken.None);
            progress?.Report(new ProgressUpdate(Summarize(phrases.Count, results.Values), result));
        }

        var summary = Summarize(phrases.Count, results.Values);
        await logger.WriteAsync("run_finished", summary, CancellationToken.None);
        return phrases.Where(results.ContainsKey).Select(phrase => results[phrase]).ToList();
    }

    public static RunSummary Summarize(int total, IEnumerable<CheckResult> source)
    {
        var items = source.ToList();
        return new RunSummary(
            total,
            items.Count,
            items.Count(item => item.Status == "nonzero"),
            items.Count(item => item.Status == "zero"),
            items.Count(item => item.Status == "error"));
    }
}
