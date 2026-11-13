using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentalControl.Core;
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
    private bool _notifyOnLogout = true;

    [ObservableProperty]
    private bool _notifyOnShutdown = true;

    [ObservableProperty]
    private bool _notifyOnLimitReached = true;

    [ObservableProperty]
    private bool _showCountdownOverlay = true;

    [ObservableProperty]
    private string _appLanguage = "ar";

    [ObservableProperty]
    private string _telegramLanguage = "ar";

    [ObservableProperty]
    private bool _autoUpdateEnabled = true;

    [ObservableProperty]
    private string _currentVersion = AppVersionInfo.VersionString;

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
        NotifyOnLogout = s.NotifyOnLogout;
        NotifyOnShutdown = s.NotifyOnShutdown;
        NotifyOnLimitReached = s.NotifyOnLimitReached;
        ShowCountdownOverlay = s.ShowCountdownOverlay;
        AppLanguage = s.AppLanguage;
        TelegramLanguage = s.TelegramLanguage;
        AutoUpdateEnabled = s.AutoUpdateEnabled;
        CurrentVersion = AppVersionInfo.VersionString;
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

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        IsBusy = true;
        StatusMessage = "Checking GitHub for updates...";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var url = $"https://api.github.com/repos/{AppVersionInfo.GitHubRepo}/releases/latest";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("ParentalControlAdmin", AppVersionInfo.VersionString));
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            using var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                StatusMessage = $"GitHub API responded with code {resp.StatusCode}.";
                MessageBox.Show(StatusMessage, "Check Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tagName = root.TryGetProperty("tag_name", out var tp) ? tp.GetString() ?? "" : "";
            var cleaned = tagName.TrimStart('v', 'V');

            if (Version.TryParse(cleaned, out var remoteVer) && remoteVer > AppVersionInfo.CurrentVersion)
            {
                StatusMessage = $"New release available: {tagName}!";
                MessageBox.Show($"New version {tagName} is available on GitHub!\nAutomatic updater will install it silently.",
                    "Update Available", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = $"You are running the latest version (v{AppVersionInfo.VersionString}).";
                MessageBox.Show(StatusMessage, "Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Check failed: {ex.Message}";
            MessageBox.Show($"Could not check for updates: {ex.Message}", "Update Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
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
            NotifyOnLogout = NotifyOnLogout,
            NotifyOnShutdown = NotifyOnShutdown,
            NotifyOnLimitReached = NotifyOnLimitReached,
            ShowCountdownOverlay = ShowCountdownOverlay,
            AppLanguage = AppLanguage,
            TelegramLanguage = TelegramLanguage,
            AutoUpdateEnabled = AutoUpdateEnabled
        });
    }
}
