using System.Text.RegularExpressions;
using ParentalControl.Core.Models;

namespace ParentalControl.Core.Data;

/// <summary>
/// Persistence for global application, Telegram, and auto-update settings in the <c>app_settings</c> table.
/// </summary>
public static partial class SettingsRepository
{
    private const string KeyTelegramEnabled = "telegram_enabled";
    private const string KeyTelegramBotToken = "telegram_bot_token";
    private const string KeyTelegramChatId = "telegram_chat_id";
    private const string KeyNotifyOnLogin = "notify_on_login";
    private const string KeyNotifyOnLogout = "notify_on_logout";
    private const string KeyNotifyOnShutdown = "notify_on_shutdown";
    private const string KeyNotifyOnLimitReached = "notify_on_limit_reached";
    private const string KeyShowCountdownOverlay = "show_countdown_overlay";
    private const string KeyAppLanguage = "app_language";
    private const string KeyTelegramLanguage = "telegram_language";
    private const string KeyAutoUpdateEnabled = "auto_update_enabled";
    private const string KeyTelegramUpdateOffset = "telegram_update_offset";

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
            NotifyOnLogout = ParseBool(map, KeyNotifyOnLogout, true),
            NotifyOnShutdown = ParseBool(map, KeyNotifyOnShutdown, true),
            NotifyOnLimitReached = ParseBool(map, KeyNotifyOnLimitReached, true),
            ShowCountdownOverlay = ParseBool(map, KeyShowCountdownOverlay, true),
            AppLanguage = NormalizeLanguage(map.GetValueOrDefault(KeyAppLanguage, "ar")),
            TelegramLanguage = NormalizeLanguage(map.GetValueOrDefault(KeyTelegramLanguage, "ar")),
            AutoUpdateEnabled = ParseBool(map, KeyAutoUpdateEnabled, true),
            TelegramUpdateOffset = ParseLong(map, KeyTelegramUpdateOffset, 0)
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
            (KeyNotifyOnLogout, settings.NotifyOnLogout ? "1" : "0"),
            (KeyNotifyOnShutdown, settings.NotifyOnShutdown ? "1" : "0"),
            (KeyNotifyOnLimitReached, settings.NotifyOnLimitReached ? "1" : "0"),
            (KeyShowCountdownOverlay, settings.ShowCountdownOverlay ? "1" : "0"),
            (KeyAppLanguage, NormalizeLanguage(settings.AppLanguage)),
            (KeyTelegramLanguage, NormalizeLanguage(settings.TelegramLanguage)),
            (KeyAutoUpdateEnabled, settings.AutoUpdateEnabled ? "1" : "0"),
            (KeyTelegramUpdateOffset, settings.TelegramUpdateOffset.ToString())
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

    public static long GetTelegramUpdateOffset()
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_settings WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", KeyTelegramUpdateOffset);
        var result = cmd.ExecuteScalar() as string;
        return long.TryParse(result, out var val) ? val : 0;
    }

    public static void SaveTelegramUpdateOffset(long offset)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO app_settings (key, value)
            VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """;
        cmd.Parameters.AddWithValue("@key", KeyTelegramUpdateOffset);
        cmd.Parameters.AddWithValue("@value", offset.ToString());
        cmd.ExecuteNonQuery();
    }

    public static string GetTelegramLanguage()
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_settings WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", KeyTelegramLanguage);
        var result = cmd.ExecuteScalar() as string;
        return NormalizeLanguage(result);
    }

    public static void SaveTelegramLanguage(string language)
    {
        var norm = NormalizeLanguage(language);
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO app_settings (key, value)
            VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """;
        cmd.Parameters.AddWithValue("@key", KeyTelegramLanguage);
        cmd.Parameters.AddWithValue("@value", norm);
        cmd.ExecuteNonQuery();
    }

    public static string NormalizeLanguage(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return "ar";
        var lower = lang.Trim().ToLowerInvariant();
        if (lower.StartsWith("en")) return "en";
        if (lower.StartsWith("fr")) return "fr";
        return "ar";
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

    private static long ParseLong(Dictionary<string, string> map, string key, long defaultValue)
    {
        if (!map.TryGetValue(key, out var raw)) return defaultValue;
        return long.TryParse(raw, out var val) ? val : defaultValue;
    }

    [GeneratedRegex(@"api\.telegram\.org/bot([^/\s?]+)", RegexOptions.IgnoreCase)]
    private static partial Regex BotUrlRegex();

    public static string? GetCustomSetting(string key)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_settings WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", key);
        var result = cmd.ExecuteScalar();
        return result?.ToString();
    }

    public static void SetCustomSetting(string key, string value)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO app_settings (key, value) VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }
}
