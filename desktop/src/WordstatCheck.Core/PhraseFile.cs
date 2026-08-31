namespace WordstatCheck.Core;

public static class PhraseFile
{
    public static async Task<IReadOnlyList<string>> ReadAsync(string path, CancellationToken token = default)
    {
        var lines = await File.ReadAllLinesAsync(path, token);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var line in lines)
        {
            var phrase = line.Trim().TrimStart('\uFEFF');
            if (phrase.Length > 0 && seen.Add(phrase))
            {
                result.Add(phrase);
            }
        }
        return result;
    }
}

