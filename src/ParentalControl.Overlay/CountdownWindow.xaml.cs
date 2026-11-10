using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ParentalControl.Core.Models;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;

namespace ParentalControl.Overlay;

public partial class CountdownWindow : Window
{
    private const string PipeName = "ParentalControl_OverlayStatus";

    private static readonly MediaBrush BrushNormal = new SolidColorBrush(MediaColor.FromRgb(0x4A, 0xDE, 0x80));
    private static readonly MediaBrush BrushWarning = new SolidColorBrush(MediaColor.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly MediaBrush BrushCritical = new SolidColorBrush(MediaColor.FromRgb(0xF8, 0x71, 0x71));

    private readonly DispatcherTimer _tickTimer;
    private readonly DispatcherTimer _syncTimer;
    private readonly int _sessionId;

    private int _remainingSeconds = -1;
    private bool _allowClose;
    private bool _isSyncing;
    private DateTime _lastRequestSentAt = DateTime.MinValue;

    public CountdownWindow()
    {
        InitializeComponent();
        _sessionId = Process.GetCurrentProcess().SessionId;

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Top + 16;

        Closing += (_, e) =>
        {
            if (!_allowClose) e.Cancel = true;
        };

        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tickTimer.Tick += (_, _) => OnLocalTick();
        _tickTimer.Start();

        _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _syncTimer.Tick += async (_, _) => await SyncStatusAsync();
        _syncTimer.Start();

        Loaded += async (_, _) => await SyncStatusAsync();
    }

    private void CardBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void RequestTimeButton_Click(object sender, RoutedEventArgs e)
    {
        if ((DateTime.Now - _lastRequestSentAt).TotalSeconds < 60)
        {
            RequestTimeButton.Content = "Wait";
            return;
        }

        RequestTimeButton.IsEnabled = false;
        RequestTimeButton.Content = "...";
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(2000);

            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, leaveOpen: true);

            await writer.WriteLineAsync($"REQUEST_TIME:{_sessionId}:15");
            var response = await reader.ReadLineAsync();
            _lastRequestSentAt = DateTime.Now;
            RequestTimeButton.Content = response == "OK" ? "Sent!" : "Wait";
        }
        catch
        {
            RequestTimeButton.Content = "Err";
        }
        finally
        {
            await Task.Delay(3000);
            RequestTimeButton.Content = "+15m";
            RequestTimeButton.IsEnabled = true;
        }
    }

    private void OnLocalTick()
    {
        if (_remainingSeconds > 0)
        {
            _remainingSeconds--;
        }

        UpdateCountdownDisplay();
    }

    private void UpdateCountdownDisplay()
    {
        if (_remainingSeconds < 0)
        {
            CountdownText.Text = "--:--:--";
            return;
        }

        var span = TimeSpan.FromSeconds(_remainingSeconds);
        CountdownText.Text = span.TotalHours >= 1
            ? $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes:D2}:{span.Seconds:D2}";

        var accent = _remainingSeconds <= 300
            ? BrushCritical
            : _remainingSeconds <= 900
                ? BrushWarning
                : BrushNormal;

        CountdownText.Foreground = accent;
        StatusDot.Fill = accent;
    }

    private async Task SyncStatusAsync()
    {
        if (_isSyncing) return;
        _isSyncing = true;

        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(1500);

            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, leaveOpen: true);

            await writer.WriteLineAsync($"STATUS:{_sessionId}");
            var json = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(json)) return;

            var status = JsonSerializer.Deserialize<OverlayStatus>(json);
            if (status is null) return;

            if (!status.ShouldShow)
            {
                _allowClose = true;
                _tickTimer.Stop();
                _syncTimer.Stop();
                Close();
                return;
            }

            _remainingSeconds = Math.Max(0, status.EffectiveSecondsRemaining);
            UsernameText.Text = status.Username;
            SubInfoText.Text = $"Until {status.ScheduleEnd} • {status.MinutesUsed}/{status.DailyMinutes}m";
            UpdateCountdownDisplay();
        }
        catch
        {
            // Service may be restarting; keep ticking locally until next poll
        }
        finally
        {
            _isSyncing = false;
        }
    }
}
