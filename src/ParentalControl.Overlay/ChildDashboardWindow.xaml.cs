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
using WpfButton = System.Windows.Controls.Button;
using WpfBrushes = System.Windows.Media.Brushes;

namespace ParentalControl.Overlay;

public partial class ChildDashboardWindow : Window
{
    private const string PipeName = "ParentalControl_OverlayStatus";

    private static readonly MediaBrush BrushNormal = new SolidColorBrush(MediaColor.FromRgb(0x38, 0xBD, 0xF8));
    private static readonly MediaBrush BrushWarning = new SolidColorBrush(MediaColor.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly MediaBrush BrushCritical = new SolidColorBrush(MediaColor.FromRgb(0xF8, 0x71, 0x71));

    private readonly DispatcherTimer _tickTimer;
    private readonly DispatcherTimer _syncTimer;
    private readonly int _sessionId;

    private int _remainingSeconds = -1;
    private DateTime _lastRequestSentAt = DateTime.MinValue;
    private string _currentLang = "ar";

    public ChildDashboardWindow()
    {
        InitializeComponent();
        _sessionId = Process.GetCurrentProcess().SessionId;

        // Default to Arabic or OS culture
        var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        _currentLang = (culture == "fr" || culture == "en") ? culture : "ar";
        ApplyLanguage(_currentLang);

        MouseDown += (_, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        };

        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tickTimer.Tick += (_, _) => OnTick();
        _tickTimer.Start();

        _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _syncTimer.Tick += async (_, _) => await SyncStatusAsync();
        _syncTimer.Start();

        Loaded += async (_, _) => await SyncStatusAsync();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void BtnLang_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton btn && btn.Tag is string lang)
        {
            _currentLang = lang;
            ApplyLanguage(_currentLang);
        }
    }

    private void ApplyLanguage(string lang)
    {
        // Update language buttons visual state
        var activeBrush = new SolidColorBrush(MediaColor.FromRgb(0x25, 0x63, 0xEB));
        var inactiveBrush = WpfBrushes.Transparent;
        var white = WpfBrushes.White;
        var muted = new SolidColorBrush(MediaColor.FromRgb(0x94, 0xA3, 0xB8));

        BtnLangAr.Background = lang == "ar" ? activeBrush : inactiveBrush;
        BtnLangAr.Foreground = lang == "ar" ? white : muted;
        BtnLangEn.Background = lang == "en" ? activeBrush : inactiveBrush;
        BtnLangEn.Foreground = lang == "en" ? white : muted;
        BtnLangFr.Background = lang == "fr" ? activeBrush : inactiveBrush;
        BtnLangFr.Foreground = lang == "fr" ? white : muted;

        this.FlowDirection = lang == "ar" ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;

        if (lang == "ar")
        {
            TxtAppTitle.Text = "لوحة وقت وأمان الطفل";
            TxtStatus.Text = "البرنامج نشط والحماية مفعلة";
            TxtRemainingLabel.Text = "الوقت المتبقي لليوم";
            TxtScheduleLabel.Text = "الأوقات المسموحة";
            TxtSessionLabel.Text = "مدة الجلسة الحالية";
            TxtSafeguards.Text = "قيود الأمان وحماية النظام وقواعد التصفح نشطة.";
            TxtRequestTitle.Text = "طلب زيادة وقت الشاشة";
            TxtRequestDesc.Text = "هل تحتاج وقتاً إضافياً للدراسة أو الواجبات؟ أرسل طلباً مباشرة لولي أمرك عبر تيليجرام.";
            BtnReq15.Content = "+15 دقيقة";
            BtnReq30.Content = "+30 دقيقة";
            BtnReq60.Content = "+1 ساعة";
        }
        else if (lang == "fr")
        {
            TxtAppTitle.Text = "Tableau de bord Enfant";
            TxtStatus.Text = "Actif & Protégé";
            TxtRemainingLabel.Text = "TEMPS RESTANT AUJOURD'HUI";
            TxtScheduleLabel.Text = "Plage horaire autorisée";
            TxtSessionLabel.Text = "Session en cours";
            TxtSafeguards.Text = "Protections système et règles de sécurité parentales actives.";
            TxtRequestTitle.Text = "Demander du temps supplémentaire";
            TxtRequestDesc.Text = "Besoin de plus de temps pour vos devoirs ? Envoyez une demande à vos parents sur Telegram.";
            BtnReq15.Content = "+15 Min";
            BtnReq30.Content = "+30 Min";
            BtnReq60.Content = "+1 Heure";
        }
        else
        {
            TxtAppTitle.Text = "Child Time & Safety Companion";
            TxtStatus.Text = "Active & Protected";
            TxtRemainingLabel.Text = "TIME LEFT TODAY";
            TxtScheduleLabel.Text = "Allowed Schedule";
            TxtSessionLabel.Text = "Current Session";
            TxtSafeguards.Text = "System safeguards and parental restrictions are active.";
            TxtRequestTitle.Text = "Request Extra Screen Time";
            TxtRequestDesc.Text = "Need more time for homework or study? Send a request to your parents via Telegram.";
            BtnReq15.Content = "+15 Min";
            BtnReq30.Content = "+30 Min";
            BtnReq60.Content = "+1 Hour";
        }
    }

    private async void BtnRequest_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton btn || btn.Tag is not string minStr || !int.TryParse(minStr, out var minutes))
            return;

        var elapsed = (DateTime.Now - _lastRequestSentAt).TotalSeconds;
        if (elapsed < 60)
        {
            var waitSec = (int)(60 - elapsed);
            TxtFeedback.Text = _currentLang switch
            {
                "ar" => $"يرجى الانتظار {waitSec} ثانية قبل إرسال طلب جديد.",
                "fr" => $"Veuillez patienter {waitSec}s avant une nouvelle demande.",
                _ => $"Please wait {waitSec}s before sending another request."
            };
            TxtFeedback.Foreground = BrushWarning;
            return;
        }

        SetRequestButtonsEnabled(false);
        TxtFeedback.Text = _currentLang switch
        {
            "ar" => "جارٍ إرسال الطلب إلى تيليجرام...",
            "fr" => "Envoi de la demande sur Telegram...",
            _ => "Sending request to Telegram..."
        };
        TxtFeedback.Foreground = BrushNormal;

        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(2500);

            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, leaveOpen: true);

            await writer.WriteLineAsync($"REQUEST_TIME:{_sessionId}:{minutes}");
            var response = await reader.ReadLineAsync();

            _lastRequestSentAt = DateTime.Now;
            if (response == "OK")
            {
                TxtFeedback.Text = _currentLang switch
                {
                    "ar" => $"✅ تم إرسال طلب (+{minutes} دقيقة) بنجاح! بانتظار موافقة ولي الأمر ⏳",
                    "fr" => $"✅ Demande (+{minutes} min) envoyée ! En attente d'approbation ⏳",
                    _ => $"✅ Request (+{minutes} min) sent! Waiting for parental approval ⏳"
                };
                TxtFeedback.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x10, 0xB9, 0x81));
            }
            else
            {
                TxtFeedback.Text = _currentLang switch
                {
                    "ar" => "تم استلام الطلب مسبقاً، يرجى الانتظار لحين المراجعة.",
                    "fr" => "Demande déjà en cours d'examen.",
                    _ => "Request already pending review."
                };
                TxtFeedback.Foreground = BrushWarning;
            }
        }
        catch
        {
            TxtFeedback.Text = _currentLang switch
            {
                "ar" => "تعذر الاتصال بخدمة الرقابة الأبوية. يرجى المحاولة لاحقاً.",
                "fr" => "Impossible de contacter le service parental.",
                _ => "Could not contact Parental Control service."
            };
            TxtFeedback.Foreground = BrushCritical;
        }
        finally
        {
            await Task.Delay(4000);
            SetRequestButtonsEnabled(true);
        }
    }

    private void SetRequestButtonsEnabled(bool enabled)
    {
        BtnReq15.IsEnabled = enabled;
        BtnReq30.IsEnabled = enabled;
        BtnReq60.IsEnabled = enabled;
    }

    private void OnTick()
    {
        if (_remainingSeconds > 0)
        {
            _remainingSeconds--;
        }
        UpdateRemainingDisplay();
    }

    private void UpdateRemainingDisplay()
    {
        if (_remainingSeconds < 0)
        {
            TxtRemainingTime.Text = "--:--:--";
            return;
        }

        var span = TimeSpan.FromSeconds(_remainingSeconds);
        TxtRemainingTime.Text = span.TotalHours >= 1
            ? $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes:D2}:{span.Seconds:D2}";

        var accent = _remainingSeconds <= 300
            ? BrushCritical
            : _remainingSeconds <= 900
                ? BrushWarning
                : BrushNormal;

        TxtRemainingTime.Foreground = accent;
    }

    private async Task SyncStatusAsync()
    {
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

            _remainingSeconds = Math.Max(0, status.EffectiveSecondsRemaining);
            TxtUserBadge.Text = string.IsNullOrWhiteSpace(status.Username) ? "PC" : status.Username;
            TxtUsageRatio.Text = $"{status.MinutesUsed}m / {status.DailyMinutes}m";

            var max = Math.Max(1, status.DailyMinutes);
            BarUsage.Maximum = max;
            BarUsage.Value = Math.Clamp(status.MinutesUsed, 0, max);

            TxtScheduleValue.Text = $"{status.ScheduleStart} - {status.ScheduleEnd}";

            var sessionMin = Math.Max(1, status.SessionMinutes);
            TxtSessionValue.Text = _currentLang switch
            {
                "ar" => $"{sessionMin} دقيقة",
                "fr" => $"{sessionMin} min",
                _ => $"{sessionMin} min"
            };

            UpdateRemainingDisplay();
        }
        catch
        {
            // Ignore temporary pipe disconnection
        }
    }
}
