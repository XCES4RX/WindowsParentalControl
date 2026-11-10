using System.ServiceProcess;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentalControl.Core.Data;

namespace ParentalControl.Admin.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string ServiceName = "ParentalControl.Service";

    private readonly DashboardViewModel _dashboardVm;
    private readonly DispatcherTimer _serviceTimer;

    [ObservableProperty]
    private ObservableObject _currentView = null!;

    [ObservableProperty]
    private string _serviceStatusText = "Service: ...";

    [ObservableProperty]
    private SolidColorBrush _serviceStatusColor = Brushes.Gray;

    [ObservableProperty]
    private bool _isServiceRunning;

    [ObservableProperty]
    private string _telegramStatusText = "Telegram: Off";

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
    }

    public void StopServicePolling() => _serviceTimer.Stop();

    private void UpdateServiceStatus()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            sc.Refresh();
            if (sc.Status == ServiceControllerStatus.Running)
            {
                ServiceStatusText = "Service: Up";
                ServiceStatusColor = Brushes.Green;
                IsServiceRunning = true;
            }
            else
            {
                ServiceStatusText = "Service: Down";
                ServiceStatusColor = Brushes.Red;
                IsServiceRunning = false;
            }
        }
        catch (InvalidOperationException)
        {
            ServiceStatusText = "Service: Down";
            ServiceStatusColor = Brushes.Red;
            IsServiceRunning = false;
        }

        UpdateTelegramStatus();
    }

    private void UpdateTelegramStatus()
    {
        try
        {
            var settings = SettingsRepository.Get();
            if (settings.TelegramEnabled && !string.IsNullOrWhiteSpace(settings.TelegramBotToken))
            {
                TelegramStatusText = string.IsNullOrWhiteSpace(settings.TelegramChatId)
                    ? "Telegram: Pairing"
                    : "Telegram: Active";
                TelegramStatusColor = string.IsNullOrWhiteSpace(settings.TelegramChatId)
                    ? Brushes.DarkOrange
                    : Brushes.DodgerBlue;
            }
            else
            {
                TelegramStatusText = "Telegram: Off";
                TelegramStatusColor = Brushes.Gray;
            }
        }
        catch
        {
            TelegramStatusText = "Telegram: Off";
            TelegramStatusColor = Brushes.Gray;
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
