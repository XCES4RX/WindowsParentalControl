using ParentalControl.Core.Data;
using ParentalControl.Core.Localization;
using ParentalControl.Core.Telegram;

namespace ParentalControl.Service;

/// <summary>
/// Sends asynchronous and high-priority parent notifications to Telegram in the configured language.
/// </summary>
public sealed class TelegramNotifier
{
    private readonly TelegramApiClient _client;
    private readonly Serilog.ILogger _logger;

    public TelegramNotifier(TelegramApiClient client, Serilog.ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    public void NotifyLogin(string username, string detail)
    {
        var settings = SettingsRepository.Get();
        if (!settings.NotifyOnLogin) return;

        var lang = settings.TelegramLanguage;
        var header = LocalizationManager.T("notify_login", lang, Escape(username), Escape(Environment.MachineName));
        var text = string.IsNullOrWhiteSpace(detail)
            ? header
            : $"{header}\n⏱ {Escape(detail)}";

        _ = SendIfEnabledAsync(text);
    }

    public void NotifyLogout(string username)
    {
        var settings = SettingsRepository.Get();
        if (!settings.NotifyOnLogout) return;

        var lang = settings.TelegramLanguage;
        var text = LocalizationManager.T("notify_logout", lang, Escape(username), Escape(Environment.MachineName));
        _ = SendIfEnabledAsync(text);
    }

    public void NotifyShutdown()
    {
        try
        {
            var settings = SettingsRepository.Get();
            if (!settings.TelegramEnabled || !settings.NotifyOnShutdown ||
                string.IsNullOrWhiteSpace(settings.TelegramBotToken) ||
                string.IsNullOrWhiteSpace(settings.TelegramChatId))
            {
                return;
            }

            var lang = settings.TelegramLanguage;
            var text = LocalizationManager.T("notify_shutdown", lang, Escape(Environment.MachineName));

            // High priority synchronous wait on shutdown before OS terminates network
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            _client.SendMessageAsync(settings.TelegramBotToken, settings.TelegramChatId, text, ct: cts.Token)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to send Telegram shutdown notification");
        }
    }

    public void NotifySleep()
    {
        var settings = SettingsRepository.Get();
        var lang = settings.TelegramLanguage;
        var text = LocalizationManager.T("notify_sleep", lang, Escape(Environment.MachineName));
        _ = SendIfEnabledAsync(text);
    }

    public void NotifyWake()
    {
        var settings = SettingsRepository.Get();
        var lang = settings.TelegramLanguage;
        var text = LocalizationManager.T("notify_wake", lang, Escape(Environment.MachineName));
        _ = SendIfEnabledAsync(text);
    }

    public void NotifyAlert(string title, string username, string detail)
    {
        var text = $"⚠️ <b>{Escape(title)}</b>\n👤 <code>{Escape(username)}</code>\n🖥 <b>{Escape(Environment.MachineName)}</b>\n📝 {Escape(detail)}";
        _ = SendIfEnabledAsync(text);
    }

    public void NotifyExtraTimeRequest(int userId, string username, int minutes)
    {
        var settings = SettingsRepository.Get();
        var lang = settings.TelegramLanguage;

        var text = LocalizationManager.T("notify_time_request", lang, Escape(username), Escape(Environment.MachineName), minutes);
        var keyboard = new
        {
            inline_keyboard = new object[][]
            {
                [
                    new { text = LocalizationManager.T("btn_approve_15", lang), callback_data = $"approve_time:{userId}:{minutes}" },
                    new { text = LocalizationManager.T("btn_approve_30", lang), callback_data = $"approve_time:{userId}:30" },
                    new { text = LocalizationManager.T("btn_deny", lang), callback_data = $"deny_time:{userId}" }
                ]
            }
        };
        _ = SendDirectAsync(text, keyboard);
    }

    public void NotifyUpdateAvailable(string versionTag, string? lang = null)
    {
        lang ??= SettingsRepository.GetTelegramLanguage();
        var text = LocalizationManager.T("update_available", lang, Escape(versionTag));
        _ = SendIfEnabledAsync(text);
    }

    public void NotifyUpdateInstalled(string versionTag, string? lang = null)
    {
        lang ??= SettingsRepository.GetTelegramLanguage();
        var text = LocalizationManager.T("update_success", lang, Escape(versionTag));
        _ = SendIfEnabledAsync(text);
    }

    public async Task SendRawIfEnabledAsync(string htmlText)
    {
        try
        {
            var settings = SettingsRepository.Get();
            if (!settings.TelegramEnabled ||
                string.IsNullOrWhiteSpace(settings.TelegramBotToken) ||
                string.IsNullOrWhiteSpace(settings.TelegramChatId))
            {
                return;
            }
            await _client.SendMessageAsync(settings.TelegramBotToken, settings.TelegramChatId, htmlText);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to send Telegram message");
        }
    }

    private async Task SendDirectAsync(string text, object? replyMarkup)
    {
        try
        {
            var settings = SettingsRepository.Get();
            if (!settings.TelegramEnabled ||
                string.IsNullOrWhiteSpace(settings.TelegramBotToken) ||
                string.IsNullOrWhiteSpace(settings.TelegramChatId))
            {
                return;
            }
            await _client.SendMessageAsync(settings.TelegramBotToken, settings.TelegramChatId, text, replyMarkup);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to send Telegram interactive notification");
        }
    }

    private async Task SendIfEnabledAsync(string text)
    {
        try
        {
            var settings = SettingsRepository.Get();
            if (!settings.TelegramEnabled ||
                string.IsNullOrWhiteSpace(settings.TelegramBotToken) ||
                string.IsNullOrWhiteSpace(settings.TelegramChatId))
            {
                return;
            }

            await _client.SendMessageAsync(settings.TelegramBotToken, settings.TelegramChatId, text);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to send Telegram notification");
        }
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
