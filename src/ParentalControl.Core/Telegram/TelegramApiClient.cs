using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ParentalControl.Core.Data;

namespace ParentalControl.Core.Telegram;

public sealed record TelegramBotInfo(long Id, string Username, string FirstName);

public sealed record TelegramUpdate(
    long UpdateId,
    string ChatId,
    string SenderName,
    string? MessageText,
    string? CallbackQueryId,
    string? CallbackData);

/// <summary>
/// Minimal HTTP client for the Telegram Bot API (no external NuGet dependencies).
/// </summary>
public sealed class TelegramApiClient
{
    private readonly HttpClient _http;

    public TelegramApiClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
    }

    private static string BuildUrl(string token, string method)
    {
        var clean = SettingsRepository.NormalizeBotToken(token);
        return $"https://api.telegram.org/bot{clean}/{method}";
    }

    public async Task<TelegramBotInfo> GetMeAsync(string token, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(BuildUrl(token, "getMe"), ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        var root = ParseOkResponse(json);

        var result = root["result"] ?? throw new InvalidOperationException("Missing result in getMe response.");
        return new TelegramBotInfo(
            result["id"]?.GetValue<long>() ?? 0,
            result["username"]?.GetValue<string>() ?? string.Empty,
            result["first_name"]?.GetValue<string>() ?? string.Empty);
    }

    public async Task<List<TelegramUpdate>> GetUpdatesAsync(
        string token,
        long offset = 0,
        int timeoutSeconds = 25,
        CancellationToken ct = default)
    {
        var url = BuildUrl(token, $"getUpdates?offset={offset}&timeout={timeoutSeconds}&allowed_updates=[\"message\",\"callback_query\"]");
        using var resp = await _http.GetAsync(url, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        var root = ParseOkResponse(json);

        var updates = new List<TelegramUpdate>();
        if (root["result"] is not JsonArray arr) return updates;

        foreach (var item in arr)
        {
            if (item is null) continue;
            var updateId = item["update_id"]?.GetValue<long>() ?? 0;

            var msg = item["message"];
            if (msg is not null)
            {
                var chatId = msg["chat"]?["id"]?.ToString() ?? string.Empty;
                var sender = msg["from"]?["first_name"]?.GetValue<string>()
                             ?? msg["from"]?["username"]?.GetValue<string>()
                             ?? "Parent";
                var text = msg["text"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(chatId))
                {
                    updates.Add(new TelegramUpdate(updateId, chatId, sender, text, null, null));
                }
                continue;
            }

            var cb = item["callback_query"];
            if (cb is not null)
            {
                var chatId = cb["message"]?["chat"]?["id"]?.ToString() ?? string.Empty;
                var sender = cb["from"]?["first_name"]?.GetValue<string>()
                             ?? cb["from"]?["username"]?.GetValue<string>()
                             ?? "Parent";
                var cbId = cb["id"]?.GetValue<string>();
                var cbData = cb["data"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(chatId))
                {
                    updates.Add(new TelegramUpdate(updateId, chatId, sender, null, cbId, cbData));
                }
            }
        }

        return updates;
    }

    public async Task SendMessageAsync(
        string token,
        string chatId,
        string text,
        object? replyMarkup = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = text,
            ["parse_mode"] = "HTML"
        };

        if (replyMarkup is not null)
        {
            payload["reply_markup"] = replyMarkup;
        }

        var body = JsonSerializer.Serialize(payload);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(BuildUrl(token, "sendMessage"), content, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        ParseOkResponse(json);
    }

    public async Task SendPhotoAsync(
        string token,
        string chatId,
        byte[] pngBytes,
        string fileName,
        string caption,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(chatId), "chat_id");
        if (!string.IsNullOrWhiteSpace(caption))
        {
            form.Add(new StringContent(caption), "caption");
        }

        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "photo", fileName);

        using var resp = await _http.PostAsync(BuildUrl(token, "sendPhoto"), form, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        ParseOkResponse(json);
    }

    public async Task AnswerCallbackQueryAsync(
        string token,
        string callbackQueryId,
        string? text = null,
        CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["callback_query_id"] = callbackQueryId,
        };
        if (!string.IsNullOrWhiteSpace(text))
        {
            payload["text"] = text;
        }

        var body = JsonSerializer.Serialize(payload);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(BuildUrl(token, "answerCallbackQuery"), content, ct);
        await resp.Content.ReadAsStringAsync(ct);
    }

    private static JsonNode ParseOkResponse(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("Empty response from Telegram API.");
        var ok = node["ok"]?.GetValue<bool>() ?? false;
        if (!ok)
        {
            var desc = node["description"]?.GetValue<string>() ?? "Unknown Telegram API error.";
            throw new InvalidOperationException(desc);
        }
        return node;
    }
}
