using System.Text.Json;

namespace WordstatCheck.Core;

public sealed class JsonLineLogger(string path)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task WriteAsync(string eventName, object data, CancellationToken token = default)
    {
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var record = JsonSerializer.Serialize(new
        {
            time = DateTimeOffset.UtcNow,
            @event = eventName,
            data
        });
        await gate.WaitAsync(token);
        try
        {
            await File.AppendAllTextAsync(path, record + Environment.NewLine, token);
        }
        finally
        {
            gate.Release();
        }
    }
}

