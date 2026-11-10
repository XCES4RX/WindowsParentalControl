using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;
using ParentalControl.Core.Telegram;

namespace ParentalControl.Admin.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly Action _navigateBack;
    private readonly TelegramApiClient _telegramClient = new();

    [ObservableProperty]
    private bool _telegramEnabled;

    [ObservableProperty]
    private string _telegramBotInput = string.Empty;

    [ObservableProperty]
    private string _telegramChatId = string.Empty;

    [ObservableProperty]
    private bool _notifyOnLogin = true;

    [ObservableProperty]
    private bool _notifyOnLimitReached = true;

    [ObservableProperty]
    private bool _showCountdownOverlay = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public SettingsViewModel(Action navigateBack)
    {
        _navigateBack = navigateBack;
    }

    public void Load()
    {
        var s = SettingsRepository.Get();
        TelegramEnabled = s.TelegramEnabled;
        TelegramBotInput = s.TelegramBotToken;
        TelegramChatId = s.TelegramChatId;
        NotifyOnLogin = s.NotifyOnLogin;
        NotifyOnLimitReached = s.NotifyOnLimitReached;
        ShowCountdownOverlay = s.ShowCountdownOverlay;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void GoBack() => _navigateBack();

    [RelayCommand]
    private void Save()
    {
        var token = SettingsRepository.NormalizeBotToken(TelegramBotInput);
        if (TelegramEnabled && string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show("Please enter a valid Telegram Bot Token or API URL before enabling Telegram.",
                "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TelegramBotInput = token;
        PersistSettings();
        StatusMessage = "Settings saved successfully.";
        MessageBox.Show("Settings saved. The background service picks up changes automatically.",
            "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task TestAndSaveAsync()
    {
        var token = SettingsRepository.NormalizeBotToken(TelegramBotInput);
        if (string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show("Enter a Telegram Bot Token or API URL first.",
                "Missing Token", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        StatusMessage = "Connecting to Telegram Bot API...";
        try
        {
            var bot = await _telegramClient.GetMeAsync(token);
            TelegramBotInput = token;
            TelegramEnabled = true;
            PersistSettings();

            if (!string.IsNullOrWhiteSpace(TelegramChatId))
            {
                await _telegramClient.SendMessageAsync(
                    token,
                    TelegramChatId.Trim(),
                    $"✅ <b>تم الاتصال بنجاح</b>\n🤖 البوت: @{bot.Username}\n🖥 الجهاز: <b>{Environment.MachineName}</b>");
                StatusMessage = $"Connected to @{bot.Username} and sent test message.";
            }
            else
            {
                StatusMessage = $"Bot @{bot.Username} verified! Now send /start to @{bot.Username} on Telegram, then click Detect Chat ID.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Telegram test failed: {ex.Message}", "Telegram Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DetectChatIdAsync()
    {
        var token = SettingsRepository.NormalizeBotToken(TelegramBotInput);
        if (string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show("Enter your Bot Token or API URL first.",
                "Missing Token", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        StatusMessage = "Checking recent Telegram messages...";
        try
        {
            var updates = await _telegramClient.GetUpdatesAsync(token, offset: 0, timeoutSeconds: 0);
            var latest = updates.LastOrDefault(u => !string.IsNullOrWhiteSpace(u.ChatId));
            if (latest is null)
            {
                StatusMessage = "No messages found yet. Open your bot in Telegram, press Start (/start), and click Detect Chat ID again.";
                MessageBox.Show(StatusMessage, "Chat ID Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TelegramBotInput = token;
            TelegramChatId = latest.ChatId;
            TelegramEnabled = true;
            PersistSettings();

            await _telegramClient.SendMessageAsync(
                token,
                TelegramChatId,
                $"✅ <b>تم ربط Chat ID بنجاح</b>\n🖥 الجهاز: <b>{Environment.MachineName}</b>");

            StatusMessage = $"Detected Chat ID {TelegramChatId} ({latest.SenderName}) and saved!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Could not detect Chat ID: {ex.Message}", "Telegram Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PersistSettings()
    {
        SettingsRepository.Save(new AppSettings
        {
            TelegramEnabled = TelegramEnabled,
            TelegramBotToken = SettingsRepository.NormalizeBotToken(TelegramBotInput),
            TelegramChatId = TelegramChatId.Trim(),
            NotifyOnLogin = NotifyOnLogin,
            NotifyOnLimitReached = NotifyOnLimitReached,
            ShowCountdownOverlay = ShowCountdownOverlay
        });
    }
}
