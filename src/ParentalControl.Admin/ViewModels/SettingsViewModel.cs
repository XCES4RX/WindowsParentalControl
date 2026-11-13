using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentalControl.Admin.Services;
using ParentalControl.Core;
using ParentalControl.Core.Data;
using ParentalControl.Core.Localization;
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

        LocalizedStrings.Instance.ApplyLanguage(AppLanguage);
    }

    partial void OnAppLanguageChanged(string value)
    {
        LocalizedStrings.Instance.ApplyLanguage(value);
        PersistSettings();
    }

    partial void OnTelegramLanguageChanged(string value)
    {
        PersistSettings();
    }

    [RelayCommand]
    private void GoBack() => _navigateBack();

    [RelayCommand]
    private void Save()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        var token = SettingsRepository.NormalizeBotToken(TelegramBotInput);
        if (TelegramEnabled && string.IsNullOrWhiteSpace(token))
        {
            var warn = lang == "en" ? "Please enter a valid Telegram Bot Token or API URL before enabling Telegram." :
                       lang == "fr" ? "Veuillez saisir un jeton de bot valide avant d'activer Telegram." :
                       "يرجى إدخال رمز بوت تلغرام أو رابط الـ API قبل تفعيل الربط.";
            MessageBox.Show(warn, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TelegramBotInput = token;
        PersistSettings();
        var savedMsg = lang == "en" ? "Settings saved successfully." :
                       lang == "fr" ? "Paramètres enregistrés avec succès." :
                       "تم حفظ الإعدادات بنجاح.";
        StatusMessage = savedMsg;
        MessageBox.Show(savedMsg, "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private async Task TestAndSaveAsync()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        var token = SettingsRepository.NormalizeBotToken(TelegramBotInput);
        if (string.IsNullOrWhiteSpace(token))
        {
            var warn = lang == "en" ? "Enter a Telegram Bot Token first." :
                       lang == "fr" ? "Entrez d'abord un jeton de bot Telegram." :
                       "أدخل رمز توكن البوت أولاً.";
            MessageBox.Show(warn, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        StatusMessage = lang == "en" ? "Connecting to Telegram Bot API..." :
                        lang == "fr" ? "Connexion à l'API Telegram..." :
                        "جاري الاتصال بـ Telegram Bot API...";
        try
        {
            var bot = await _telegramClient.GetMeAsync(token);
            TelegramBotInput = token;
            TelegramEnabled = true;
            PersistSettings();

            if (!string.IsNullOrWhiteSpace(TelegramChatId))
            {
                var testMsg = lang == "en" ? $"✅ <b>Connected successfully</b>\n🤖 Bot: @{bot.Username}\n🖥 Device: <b>{Environment.MachineName}</b>" :
                              lang == "fr" ? $"✅ <b>Connexion réussie</b>\n🤖 Bot : @{bot.Username}\n🖥 Appareil : <b>{Environment.MachineName}</b>" :
                              $"✅ <b>تم الاتصال بنجاح</b>\n🤖 البوت: @{bot.Username}\n🖥 الجهاز: <b>{Environment.MachineName}</b>";
                await _telegramClient.SendMessageAsync(token, TelegramChatId.Trim(), testMsg);
                StatusMessage = lang == "en" ? $"Connected to @{bot.Username} and sent test message." :
                                lang == "fr" ? $"Connecté à @{bot.Username} et message test envoyé." :
                                $"تم الاتصال بـ @{bot.Username} وإرسال رسالة اختبار.";
            }
            else
            {
                StatusMessage = lang == "en" ? $"Bot @{bot.Username} verified! Send /start to @{bot.Username} on Telegram, then click Detect Chat ID." :
                                lang == "fr" ? $"Bot @{bot.Username} vérifié ! Envoyez /start à @{bot.Username} puis cliquez sur Détecter." :
                                $"تم التحقق من @{bot.Username}! أرسل /start للبوت ثم اضغط زر اكتشاف المعرف.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show(ex.Message, "Telegram Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DetectChatIdAsync()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        var token = SettingsRepository.NormalizeBotToken(TelegramBotInput);
        if (string.IsNullOrWhiteSpace(token))
        {
            var warn = lang == "en" ? "Enter your Bot Token first." :
                       lang == "fr" ? "Entrez d'abord votre jeton de bot." :
                       "أدخل رمز توكن البوت أولاً.";
            MessageBox.Show(warn, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsBusy = true;
        StatusMessage = lang == "en" ? "Checking recent Telegram messages..." :
                        lang == "fr" ? "Recherche des messages récents..." :
                        "جاري فحص رسائل تلغرام الأخيرة...";
        try
        {
            var updates = await _telegramClient.GetUpdatesAsync(token, offset: 0, timeoutSeconds: 0);
            var latest = updates.LastOrDefault(u => !string.IsNullOrWhiteSpace(u.ChatId));
            if (latest is null)
            {
                var notFound = lang == "en" ? "No messages found. Send /start to your bot in Telegram, then click Detect again." :
                               lang == "fr" ? "Aucun message trouvé. Envoyez /start à votre bot puis réessayez." :
                               "لم يتم العثور على رسائل. أرسل /start للبوت في تلغرام ثم اضغط اكتشاف مجدداً.";
                StatusMessage = notFound;
                MessageBox.Show(notFound, "Chat ID", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TelegramBotInput = token;
            TelegramChatId = latest.ChatId;
            TelegramEnabled = true;
            PersistSettings();

            var linkedMsg = lang == "en" ? $"✅ <b>Chat ID linked successfully</b>\n🖥 Device: <b>{Environment.MachineName}</b>" :
                            lang == "fr" ? $"✅ <b>Chat ID associé avec succès</b>\n🖥 Appareil : <b>{Environment.MachineName}</b>" :
                            $"✅ <b>تم ربط Chat ID بنجاح</b>\n🖥 الجهاز: <b>{Environment.MachineName}</b>";
            await _telegramClient.SendMessageAsync(token, TelegramChatId, linkedMsg);

            StatusMessage = lang == "en" ? $"Detected Chat ID {TelegramChatId} and saved!" :
                            lang == "fr" ? $"Chat ID {TelegramChatId} détecté et enregistré !" :
                            $"تم اكتشاف Chat ID {TelegramChatId} وحفظه بنجاح!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show(ex.Message, "Telegram Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        IsBusy = true;
        StatusMessage = lang == "en" ? "Checking GitHub for updates..." :
                        lang == "fr" ? "Vérification des mises à jour sur GitHub..." :
                        "جاري فحص التحديثات على GitHub...";
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
                StatusMessage = $"GitHub API: {resp.StatusCode}";
                return;
            }

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tagName = root.TryGetProperty("tag_name", out var tp) ? tp.GetString() ?? "" : "";
            var cleaned = tagName.TrimStart('v', 'V');

            if (Version.TryParse(cleaned, out var remoteVer) && remoteVer > AppVersionInfo.CurrentVersion)
            {
                StatusMessage = LocalizationManager.T("update_available", lang, tagName);
                var alert = lang == "en" ? $"New version {tagName} is available!\nAutomatic updater will install it silently." :
                            lang == "fr" ? $"Nouvelle version {tagName} disponible !\nLe programme sera mis à jour silencieusement." :
                            $"الإصدار الجديد {tagName} متاح الآن!\nسيقوم المحدث التلقائي بتثبيته بصمت.";
                MessageBox.Show(alert, "Update Available", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = LocalizationManager.T("update_no_new", lang, AppVersionInfo.VersionString);
                var upToDate = lang == "en" ? $"You are running the latest version (v{AppVersionInfo.VersionString})." :
                               lang == "fr" ? $"Vous utilisez la dernière version (v{AppVersionInfo.VersionString})." :
                               $"أنت تستخدم بالفعل أحدث إصدار (v{AppVersionInfo.VersionString}).";
                MessageBox.Show(upToDate, "Up to Date", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Check failed: {ex.Message}";
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
