using System.ServiceProcess;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentalControl.Admin.Services;
using ParentalControl.Core.Data;
using ParentalControl.Core.Localization;

namespace ParentalControl.Admin.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string ServiceName = "ParentalControl.Service";

    private readonly DashboardViewModel _dashboardVm;
    private readonly DispatcherTimer _serviceTimer;

    [ObservableProperty]
    private ObservableObject _currentView = null!;

    [ObservableProperty]
    private string _serviceStatusText = "...";

    [ObservableProperty]
    private SolidColorBrush _serviceStatusColor = Brushes.Gray;

    [ObservableProperty]
    private bool _isServiceRunning;

    [ObservableProperty]
    private string _telegramStatusText = "...";

    [ObservableProperty]
    private SolidColorBrush _telegramStatusColor = Brushes.Gray;

    public MainViewModel()
    {
        _dashboardVm = new DashboardViewModel(NavigateToUserDetail);
        CurrentView = _dashboardVm;
        _dashboardVm.LoadAll();

        UpdateServiceStatus();
        _serviceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _serviceTimer.Tick += (_, _) => UpdateServiceStatus();
        _serviceTimer.Start();

        // Listen for language changes to refresh headers
        LocalizedStrings.Instance.PropertyChanged += (_, _) =>
        {
            UpdateServiceStatus();
            _dashboardVm.LoadAll();
        };
    }

    public void StopServicePolling() => _serviceTimer.Stop();

    private void UpdateServiceStatus()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        try
        {
            using var sc = new ServiceController(ServiceName);
            sc.Refresh();
            if (sc.Status == ServiceControllerStatus.Running)
            {
                ServiceStatusText = LocalizationManager.T("service_active", lang);
                ServiceStatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Modern Emerald Green
                IsServiceRunning = true;
            }
            else
            {
                ServiceStatusText = LocalizationManager.T("service_down", lang);
                ServiceStatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Modern Crimson
                IsServiceRunning = false;
            }
        }
        catch (InvalidOperationException)
        {
            ServiceStatusText = LocalizationManager.T("service_down", lang);
            ServiceStatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            IsServiceRunning = false;
        }

        UpdateTelegramStatus();
    }

    private void UpdateTelegramStatus()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        try
        {
            var settings = SettingsRepository.Get();
            if (settings.TelegramEnabled && !string.IsNullOrWhiteSpace(settings.TelegramBotToken))
            {
                if (string.IsNullOrWhiteSpace(settings.TelegramChatId))
                {
                    TelegramStatusText = LocalizationManager.T("telegram_pairing", lang);
                    TelegramStatusColor = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
                }
                else
                {
                    TelegramStatusText = LocalizationManager.T("telegram_active", lang);
                    TelegramStatusColor = new SolidColorBrush(Color.FromRgb(14, 165, 233)); // Sky Blue
                }
            }
            else
            {
                TelegramStatusText = LocalizationManager.T("telegram_off", lang);
                TelegramStatusColor = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Slate gray
            }
        }
        catch
        {
            TelegramStatusText = LocalizationManager.T("telegram_off", lang);
            TelegramStatusColor = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsVm = new SettingsViewModel(NavigateBack);
        CurrentView = settingsVm;
        settingsVm.Load();
    }

    private void NavigateToUserDetail(UserRow user)
    {
        var detailVm = new UserDetailViewModel(user, _dashboardVm.SidToUsername, NavigateBack);
        CurrentView = detailVm;
        detailVm.LoadAll();
    }

    private void NavigateBack()
    {
        UpdateTelegramStatus();
        _dashboardVm.LoadAll();
        CurrentView = _dashboardVm;
    }
}
