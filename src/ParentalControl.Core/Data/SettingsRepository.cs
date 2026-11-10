using System.Text.RegularExpressions;
using ParentalControl.Core.Models;

namespace ParentalControl.Core.Data;

/// <summary>
/// Persistence for global application and Telegram settings in the <c>app_settings</c> table.
/// </summary>
public static partial class SettingsRepository
{
    private const string KeyTelegramEnabled = "telegram_enabled";
    private const string KeyTelegramBotToken = "telegram_bot_token";
    private const string KeyTelegramChatId = "telegram_chat_id";
    private const string KeyNotifyOnLogin = "notify_on_login";
    private const string KeyNotifyOnLimitReached = "notify_on_limit_reached";
    private const string KeyShowCountdownOverlay = "show_countdown_overlay";

    public static AppSettings Get()
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM app_settings";

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            map[reader.GetString(0)] = reader.GetString(1);
        }

        return new AppSettings
        {
            TelegramEnabled = ParseBool(map, KeyTelegramEnabled, false),
            TelegramBotToken = map.GetValueOrDefault(KeyTelegramBotToken, string.Empty),
            TelegramChatId = map.GetValueOrDefault(KeyTelegramChatId, string.Empty),
            NotifyOnLogin = ParseBool(map, KeyNotifyOnLogin, true),
            NotifyOnLimitReached = ParseBool(map, KeyNotifyOnLimitReached, true),
            ShowCountdownOverlay = ParseBool(map, KeyShowCountdownOverlay, true),
        };
    }

    public static void Save(AppSettings settings)
    {
        var normalizedToken = NormalizeBotToken(settings.TelegramBotToken);

        using var connection = DatabaseManager.CreateConnection();
        using var transaction = connection.BeginTransaction();

        var pairs = new (string Key, string Value)[]
        {
            (KeyTelegramEnabled, settings.TelegramEnabled ? "1" : "0"),
            (KeyTelegramBotToken, normalizedToken),
            (KeyTelegramChatId, settings.TelegramChatId.Trim()),
            (KeyNotifyOnLogin, settings.NotifyOnLogin ? "1" : "0"),
            (KeyNotifyOnLimitReached, settings.NotifyOnLimitReached ? "1" : "0"),
            (KeyShowCountdownOverlay, settings.ShowCountdownOverlay ? "1" : "0"),
        };

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO app_settings (key, value)
            VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """;
        var keyParam = cmd.Parameters.Add("@key", Microsoft.Data.Sqlite.SqliteType.Text);
        var valParam = cmd.Parameters.Add("@value", Microsoft.Data.Sqlite.SqliteType.Text);

        foreach (var (key, value) in pairs)
        {
            keyParam.Value = key;
            valParam.Value = value;
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <summary>
    /// Accepts either a raw Telegram bot token or a full Telegram Bot API URL
    /// (e.g. https://api.telegram.org/bot123456:ABC.../getMe) and extracts the token.
    /// </summary>
    public static string NormalizeBotToken(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput)) return string.Empty;

        var trimmed = rawInput.Trim();
        var match = BotUrlRegex().Match(trimmed);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        if (trimmed.StartsWith("bot", StringComparison.OrdinalIgnoreCase) && trimmed.Contains(':'))
        {
            return trimmed[3..];
        }

        return trimmed;
    }

    private static bool ParseBool(Dictionary<string, string> map, string key, bool defaultValue)
    {
        if (!map.TryGetValue(key, out var raw)) return defaultValue;
        return raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"api\.telegram\.org/bot([^/\s?]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BotUrlRegex();
}
