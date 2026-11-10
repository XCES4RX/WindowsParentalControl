namespace ParentalControl.Core.Models;

/// <summary>
/// Global application, Telegram bot, and overlay settings stored in SQLite.
/// </summary>
public sealed class AppSettings
{
    public bool TelegramEnabled { get; set; }
    public string TelegramBotToken { get; set; } = string.Empty;
    public string TelegramChatId { get; set; } = string.Empty;
    public bool NotifyOnLogin { get; set; } = true;
    public bool NotifyOnLimitReached { get; set; } = true;
    public bool ShowCountdownOverlay { get; set; } = true;
}
