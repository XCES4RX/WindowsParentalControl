using ParentalControl.Core.Data;
using ParentalControl.Core.Telegram;

namespace ParentalControl.Service;

/// <summary>
/// Sends asynchronous parent notifications to Telegram when enabled in settings.
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
        var text = string.IsNullOrWhiteSpace(detail)
            ? $"🟢 <b>تسجيل دخول:</b> <code>{Escape(username)}</code> على جهاز <b>{Escape(Environment.MachineName)}</b>"
            : $"🟢 <b>تسجيل دخول:</b> <code>{Escape(username)}</code> على جهاز <b>{Escape(Environment.MachineName)}</b>\n⏱ {Escape(detail)}";

        _ = SendIfEnabledAsync(text, isLoginEvent: true);
    }

    public void NotifyAlert(string title, string username, string detail)
    {
        var text = $"⚠️ <b>{Escape(title)}</b>\n👤 المستخدم: <code>{Escape(username)}</code>\n🖥 الجهاز: <b>{Escape(Environment.MachineName)}</b>\n📝 {Escape(detail)}";
        _ = SendIfEnabledAsync(text, isLoginEvent: false);
    }

    public void NotifyExtraTimeRequest(int userId, string username, int minutes)
    {
        var text = $"🙋 <b>طلب وقت إضافي</b>\n👤 الطفل: <code>{Escape(username)}</code>\n🖥 الجهاز: <b>{Escape(Environment.MachineName)}</b>\n⏱ يطلب: <b>+{minutes} دقيقة</b>";
        var keyboard = new
        {
            inline_keyboard = new object[][]
            {
                [
                    new { text = $"✅ موافقة (+{minutes} د)", callback_data = $"approve_time:{userId}:{minutes}" },
                    new { text = "✅ +30 د", callback_data = $"approve_time:{userId}:30" },
                    new { text = "❌ رفض", callback_data = $"deny_time:{userId}" }
                ]
            }
        };
        _ = SendDirectAsync(text, keyboard);
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
            _logger.Warning(ex, "Failed to send Telegram summary");
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

    private async Task SendIfEnabledAsync(string text, bool isLoginEvent)
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

            if (isLoginEvent && !settings.NotifyOnLogin) return;
            if (!isLoginEvent && !settings.NotifyOnLimitReached) return;

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
