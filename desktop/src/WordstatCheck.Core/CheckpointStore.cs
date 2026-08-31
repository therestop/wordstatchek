using System.Text.Json;

namespace WordstatCheck.Core;

public sealed class CheckpointStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public string Path { get; } = path;

    public async Task<Dictionary<string, CheckResult>> LoadAsync(CancellationToken token = default)
    {
        if (!File.Exists(Path))
        {
            return new Dictionary<string, CheckResult>(StringComparer.Ordinal);
        }

        await using var stream = File.OpenRead(Path);
        var payload = await JsonSerializer.DeserializeAsync<CheckpointPayload>(stream, JsonOptions, token)
            ?? throw new InvalidDataException("Checkpoint повреждён");
        if (payload.Version != 1)
        {
            throw new InvalidDataException("Неподдерживаемая версия checkpoint");
        }
        return payload.Results.ToDictionary(item => item.Phrase, StringComparer.Ordinal);
    }

    public async Task SaveAsync(IEnumerable<CheckResult> results, CancellationToken token = default)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var temporary = Path + ".tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new CheckpointPayload(1, results.ToList()),
                JsonOptions,
                token);
        }
        File.Move(temporary, Path, true);
    }

    private sealed record CheckpointPayload(int Version, List<CheckResult> Results);
}

