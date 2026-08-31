using System.Text.Json;
using System.Text.RegularExpressions;

namespace WordstatCheck.Core;

public sealed record RegionOption(string Id, string Name, string Path);

public sealed record RegionCatalogResult(
    IReadOnlyList<RegionOption> Regions,
    bool IsFallback,
    string Source);

public sealed class RegionCatalog(HttpClient httpClient, string cachePath)
{
    public const string SourceUrl = "https://wordstat.yandex.ru/";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(30);
    private static readonly JsonSerializerOptions CacheJsonOptions = new() { WriteIndented = true };

    public async Task<RegionCatalogResult> LoadAsync(CancellationToken token = default)
    {
        var cached = await TryReadCacheAsync(token);
        if (cached is not null && IsFreshCache())
        {
            return new RegionCatalogResult(cached, false, "cache");
        }

        try
        {
            var html = await httpClient.GetStringAsync(SourceUrl, token);
            var regions = ParseWordstatPage(html);
            if (regions.Count == 0)
            {
                throw new InvalidDataException("Официальный каталог Wordstat не содержит регионов.");
            }
            try
            {
                await WriteCacheAsync(regions, token);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Каталог уже загружен: ошибка кеша не должна лишать пользователя выбора.
            }
            return new RegionCatalogResult(regions, false, "wordstat.yandex.ru");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
        {
            if (cached is not null)
            {
                return new RegionCatalogResult(cached, false, "stale-cache");
            }
            return new RegionCatalogResult(FallbackRegions, true, "built-in");
        }
    }

    public static IReadOnlyList<RegionOption> ParseWordstatPage(string html)
    {
        var match = Regex.Match(
            html,
            "\\\"regions\\\"\\s*:\\s*(?<object>\\{)\\s*\\\"acceptableRegionValues\\\"",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new InvalidDataException("Не найден каталог регионов Wordstat.");
        }

        var objectStart = match.Groups["object"].Index;
        var objectEnd = FindJsonObjectEnd(html, objectStart);
        using var document = JsonDocument.Parse(html[objectStart..(objectEnd + 1)]);
        var root = document.RootElement;
        var acceptable = root.GetProperty("acceptableRegionValues")
            .EnumerateArray()
            .Select(item => item.GetString() ?? "")
            .Where(value => value.Length > 0 && value != "all")
            .ToHashSet(StringComparer.Ordinal);

        var regions = new Dictionary<string, RegionOption>(StringComparer.Ordinal);
        foreach (var node in root.GetProperty("tree").EnumerateArray())
        {
            Flatten(node, [], acceptable, regions);
        }

        return regions.Values
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Path, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static int FindJsonObjectEnd(string source, int start)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var index = start; index < source.Length; index++)
        {
            var character = source[index];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == '"') inString = false;
                continue;
            }

            if (character == '"') inString = true;
            else if (character == '{') depth++;
            else if (character == '}' && --depth == 0) return index;
        }
        throw new InvalidDataException("Каталог регионов Wordstat повреждён.");
    }

    private static void Flatten(
        JsonElement node,
        IReadOnlyList<string> parents,
        HashSet<string> acceptable,
        Dictionary<string, RegionOption> target)
    {
        var id = ReadString(node.GetProperty("value"));
        var name = node.GetProperty("label").GetString()?.Trim() ?? "";
        var path = parents.Append(name).Where(value => value.Length > 0).ToList();

        if (id.Length > 0 && name.Length > 0 && acceptable.Contains(id))
        {
            target[id] = new RegionOption(id, name, string.Join(" / ", path));
        }

        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                Flatten(child, path, acceptable, target);
            }
        }
    }

    private static string ReadString(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? value.GetString() ?? ""
        : value.GetRawText();

    private bool IsFreshCache() => File.Exists(cachePath)
        && DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) < CacheLifetime;

    private async Task<IReadOnlyList<RegionOption>?> TryReadCacheAsync(CancellationToken token)
    {
        if (!File.Exists(cachePath)) return null;
        try
        {
            await using var stream = File.OpenRead(cachePath);
            var cached = await JsonSerializer.DeserializeAsync<List<RegionOption>>(stream, cancellationToken: token);
            return cached is { Count: > 0 } ? cached : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private async Task WriteCacheAsync(IReadOnlyList<RegionOption> regions, CancellationToken token)
    {
        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporary = cachePath + ".tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, regions, CacheJsonOptions, token);
        }
        File.Move(temporary, cachePath, true);
    }

    private static readonly IReadOnlyList<RegionOption> FallbackRegions =
    [
        new("225", "Россия", "Россия"),
        new("1", "Москва и область", "Россия / Москва и область"),
        new("213", "Москва", "Россия / Москва и область / Москва"),
        new("2", "Санкт-Петербург", "Россия / Северо-Запад / Санкт-Петербург"),
        new("54", "Екатеринбург", "Россия / Урал / Екатеринбург"),
        new("65", "Новосибирск", "Россия / Сибирь / Новосибирск"),
        new("43", "Казань", "Россия / Поволжье / Казань"),
        new("47", "Нижний Новгород", "Россия / Поволжье / Нижний Новгород"),
        new("35", "Краснодар", "Россия / Юг / Краснодар"),
        new("39", "Ростов-на-Дону", "Россия / Юг / Ростов-на-Дону"),
        new("172", "Уфа", "Россия / Поволжье / Уфа"),
        new("62", "Красноярск", "Россия / Сибирь / Красноярск"),
        new("75", "Владивосток", "Россия / Дальний Восток / Владивосток")
    ];
}
