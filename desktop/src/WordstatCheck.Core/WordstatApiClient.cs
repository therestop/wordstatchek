using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WordstatCheck.Core;

public sealed class WordstatApiException(string message, bool fatal = false) : Exception(message)
{
    public bool Fatal { get; } = fatal;
}

public sealed class WordstatApiClient(HttpClient httpClient, WordstatOptions options)
{
    public const string Endpoint = "https://searchapi.api.cloud.yandex.net/v2/wordstat/topRequests";
    private static readonly HashSet<HttpStatusCode> Retryable =
    [
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    ];

    public async Task<long> GetTotalCountAsync(string phrase, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.FolderId))
        {
            throw new WordstatApiException("API-ключ и Folder ID обязательны", true);
        }

        string lastError = "Wordstat API не ответил";
        var attempts = Math.Max(1, options.Attempts);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Api-Key", options.ApiKey.Trim());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = JsonContent.Create(new TopRequest(
                phrase,
                options.NumPhrases,
                options.Regions,
                options.Devices,
                options.FolderId.Trim()));

            HttpResponseMessage response;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(options.Timeout);
                response = await httpClient.SendAsync(request, timeout.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                lastError = "Превышено время ожидания Wordstat API";
                if (attempt == attempts) break;
                await DelayAsync(attempt, null, token);
                continue;
            }
            catch (HttpRequestException error)
            {
                lastError = $"Сетевая ошибка: {error.Message}";
                if (attempt == attempts) break;
                await DelayAsync(attempt, null, token);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadFromJsonAsync<TopResponse>(cancellationToken: token)
                        ?? throw new WordstatApiException("Пустой ответ Wordstat API");
                    return payload.TotalCount
                        ?? throw new WordstatApiException("В ответе Wordstat API отсутствует totalCount");
                }

                var message = await ReadErrorAsync(response, token);
                lastError = $"HTTP {(int)response.StatusCode}: {message}";
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new WordstatApiException(
                        lastError + ". Проверьте API-ключ, scope, роль и Folder ID.", true);
                }
                if (!Retryable.Contains(response.StatusCode))
                {
                    throw new WordstatApiException(lastError);
                }
                if (attempt < attempts)
                {
                    await DelayAsync(attempt, response.Headers.RetryAfter?.Delta, token);
                }
            }
        }
        throw new WordstatApiException(lastError);
    }

    private async Task DelayAsync(int attempt, TimeSpan? retryAfter, CancellationToken token)
    {
        var delay = retryAfter ?? TimeSpan.FromSeconds(
            Math.Min(options.MaxDelay.TotalSeconds, options.BaseDelay.TotalSeconds * Math.Pow(2, attempt - 1)));
        await Task.Delay(delay, token);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken token)
    {
        var text = await response.Content.ReadAsStringAsync(token);
        if (string.IsNullOrWhiteSpace(text)) return response.ReasonPhrase ?? "Ошибка API";
        try
        {
            using var json = JsonDocument.Parse(text);
            if (json.RootElement.TryGetProperty("message", out var message)) return message.GetString() ?? text;
            if (json.RootElement.TryGetProperty("error", out var error)) return error.ToString();
        }
        catch (JsonException) { }
        return text.Length > 300 ? text[..300] : text;
    }

    private sealed record TopRequest(
        [property: JsonPropertyName("phrase")] string Phrase,
        [property: JsonPropertyName("numPhrases")] int NumPhrases,
        [property: JsonPropertyName("regions")] IReadOnlyList<string> Regions,
        [property: JsonPropertyName("devices")] IReadOnlyList<string> Devices,
        [property: JsonPropertyName("folderId")] string FolderId);

    private sealed record TopResponse(
        [property: JsonPropertyName("totalCount")] long? TotalCount);
}
