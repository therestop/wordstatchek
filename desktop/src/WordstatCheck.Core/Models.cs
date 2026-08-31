namespace WordstatCheck.Core;

public sealed record CheckResult(
    string Phrase,
    long? TotalCount,
    string Status,
    string Error,
    DateTimeOffset CheckedAt);

public sealed record RunSummary(int Total, int Processed, int Nonzero, int Zero, int Errors);

public sealed record ProgressUpdate(RunSummary Summary, CheckResult? LastResult);

public sealed class WordstatOptions
{
    public required string ApiKey { get; init; }
    public required string FolderId { get; init; }
    public int NumPhrases { get; init; } = 1;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public int Attempts { get; init; } = 6;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(60);
    public IReadOnlyList<string> Regions { get; init; } = [];
    public IReadOnlyList<string> Devices { get; init; } = [];
}

