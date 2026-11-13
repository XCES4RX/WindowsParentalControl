using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentalControl.Admin.Services;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;
using ParentalControl.Core.Platform;

namespace ParentalControl.Admin.ViewModels;

public partial class UserDetailViewModel : ObservableObject
{
    private readonly Dictionary<string, string> _sidToUsername;
    private readonly Action _navigateBack;

    public UserRow User { get; }

    [ObservableProperty]
    private int _dailyMinutes = 120;

    [ObservableProperty]
    private string _scheduleStart = "08:00";

    [ObservableProperty]
    private string _scheduleEnd = "22:00";

    [ObservableProperty]
    private int _editableUsageMinutes;

    [ObservableProperty]
    private bool _lockTaskbar;

    [ObservableProperty]
    private bool _blockSettings;

    [ObservableProperty]
    private bool _blockTaskManager;

    [ObservableProperty]
    private bool _blockCommandPrompt;

    [ObservableProperty]
    private string _blockedAppsText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<EventRecord> _eventRecords = [];

    public UserDetailViewModel(UserRow user, Dictionary<string, string> sidToUsername, Action navigateBack)
    {
        User = user;
        _sidToUsername = sidToUsername;
        _navigateBack = navigateBack;
    }

    public void LoadAll()
    {
        RefreshUser();
        LoadLimitFields();
        LoadPolicyFields();
        RefreshEvents();
        EditableUsageMinutes = User.TodayMinutesUsed;
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigateBack();
    }

    [RelayCommand]
    private void RefreshUser()
    {
        User.RefreshUsage();
        User.RefreshLimits();
        LoadLimitFields();
        LoadPolicyFields();
        EditableUsageMinutes = User.TodayMinutesUsed;
    }

    [RelayCommand]
    private void RefreshEvents()
    {
        var events = EventRepository.GetEvents(userSid: User.Sid);
        foreach (var evt in events)
        {
            evt.Username = _sidToUsername.TryGetValue(evt.UserSid, out var name) ? name : evt.UserSid;
        }
        EventRecords = new ObservableCollection<EventRecord>(events);
    }

    [RelayCommand]
    private void SetUsage()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        if (EditableUsageMinutes < 0)
        {
            var warn = lang == "en" ? "Usage minutes cannot be negative." :
                       lang == "fr" ? "Les minutes d'utilisation ne peuvent pas être négatives." :
                       "دقائق الاستخدام لا يمكن أن تكون قيمة سالبة.";
            MessageBox.Show(warn, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        UsageRepository.SetUsage(User.Id, today, EditableUsageMinutes);
        User.RefreshUsage();
        EditableUsageMinutes = User.TodayMinutesUsed;

        var success = lang == "en" ? "Today's usage updated." :
                      lang == "fr" ? "Utilisation mise à jour." :
                      "تم تحديث استخدام اليوم بنجاح.";
        MessageBox.Show(success, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void SetLimits()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        if (!TimeOnly.TryParse(ScheduleStart, out var start) ||
            !TimeOnly.TryParse(ScheduleEnd, out var end))
        {
            var warn = lang == "en" ? "Invalid time format. Use HH:mm (e.g. 08:00)." :
                       lang == "fr" ? "Format horaire invalide. Utilisez HH:mm (ex. 08:00)." :
                       "صيغة الوقت غير صحيحة. استخدم HH:mm مثل 08:00.";
            MessageBox.Show(warn, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (start >= end)
        {
            var warn = lang == "en" ? "Schedule start must be before schedule end." :
                       lang == "fr" ? "L'heure de début doit précéder l'heure de fin." :
                       "يجب أن تكون بداية الجدول قبل نهايته.";
            MessageBox.Show(warn, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (DailyMinutes < 1 || DailyMinutes > 1440)
        {
            var warn = lang == "en" ? "Daily minutes must be between 1 and 1440." :
                       lang == "fr" ? "Les minutes quotidiennes doivent être comprises entre 1 et 1440." :
                       "الحد اليومي بالدقائق يجب أن يكون بين 1 و1440 دقيقة.";
            MessageBox.Show(warn, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LimitRepository.Upsert(new LimitConfig
        {
            UserId = User.Id,
            DailyMinutes = DailyMinutes,
            ScheduleStart = start,
            ScheduleEnd = end
        });
        UserRepository.SetRestricted(User.Id, true);
        User.IsRestricted = true;
        User.RefreshLimits();

        var ok = lang == "en" ? "Time limits saved successfully." :
                 lang == "fr" ? "Limites de temps enregistrées avec succès." :
                 "تم حفظ إعدادات الوقت والجدول بنجاح.";
        MessageBox.Show(ok, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void RemoveLimits()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        var confirm = lang == "en" ? $"Remove all time limits for {User.Username}?" :
                      lang == "fr" ? $"Supprimer toutes les limites de temps pour {User.Username} ?" :
                      $"هل تريد حقاً إزالة كافة الحدود الزمنية للمستخدم {User.Username}؟";
        var result = MessageBox.Show(confirm, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        LimitRepository.Delete(User.Id);
        UserRepository.SetRestricted(User.Id, false);
        User.IsRestricted = false;
        User.RefreshLimits();

        DailyMinutes = 120;
        ScheduleStart = "08:00";
        ScheduleEnd = "22:00";
    }

    [RelayCommand]
    private void SavePolicies()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        var policy = new UserPolicyConfig
        {
            UserId = User.Id,
            LockTaskbar = LockTaskbar,
            BlockSettings = BlockSettings,
            BlockTaskManager = BlockTaskManager,
            BlockCommandPrompt = BlockCommandPrompt
        };

        UserPolicyRepository.Upsert(policy);
        var appliedNow = WindowsPolicyManager.ApplyPolicies(User.Sid, policy);

        var status = appliedNow
            ? (lang == "en" ? "Restrictions saved and applied to active session." :
               lang == "fr" ? "Restrictions enregistrées et appliquées à la session active." :
               "تم حفظ القيود وتطبيقها فوراً على الجلسة النشطة.")
            : (lang == "en" ? "Restrictions saved. They will apply on next sign-in." :
               lang == "fr" ? "Restrictions enregistrées. Elles s'appliqueront à la prochaine connexion." :
               "تم حفظ القيود. ستطبّق تلقائياً عند تسجيل الدخول القادم.");
        MessageBox.Show(status, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ClearPolicies()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        var confirm = lang == "en" ? $"Clear all Windows restrictions for {User.Username}?" :
                      lang == "fr" ? $"Supprimer toutes les restrictions Windows pour {User.Username} ?" :
                      $"هل تريد إلغاء كافة قيود ويندوز للمستخدم {User.Username}؟";
        var result = MessageBox.Show(confirm, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        UserPolicyRepository.Delete(User.Id);
        LockTaskbar = false;
        BlockSettings = false;
        BlockTaskManager = false;
        BlockCommandPrompt = false;
        WindowsPolicyManager.ApplyPolicies(User.Sid, null);

        var msg = lang == "en" ? "Windows restrictions cleared." :
                  lang == "fr" ? "Restrictions Windows effacées." :
                  "تم إلغاء كافة قيود ويندوز بنجاح.";
        MessageBox.Show(msg, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadPolicyFields()
    {
        var policy = UserPolicyRepository.GetByUserId(User.Id);
        LockTaskbar = policy?.LockTaskbar ?? false;
        BlockSettings = policy?.BlockSettings ?? false;
        BlockTaskManager = policy?.BlockTaskManager ?? false;
        BlockCommandPrompt = policy?.BlockCommandPrompt ?? false;

        var apps = BlockedAppRepository.GetByUserId(User.Id);
        BlockedAppsText = string.Join(", ", apps);
    }

    [RelayCommand]
    private void SaveBlockedApps()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        var names = BlockedAppsText.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        BlockedAppRepository.ReplaceAll(User.Id, names);
        BlockedAppsText = string.Join(", ", BlockedAppRepository.GetByUserId(User.Id));

        var msg = lang == "en" ? "Blocked applications saved." :
                  lang == "fr" ? "Applications bloquées enregistrées." :
                  "تم حفظ قائمة التطبيقات المحظورة بنجاح.";
        MessageBox.Show(msg, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ClearBlockedApps()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        BlockedAppRepository.ReplaceAll(User.Id, []);
        BlockedAppsText = string.Empty;

        var msg = lang == "en" ? "All blocked applications cleared." :
                  lang == "fr" ? "Toutes les applications bloquées ont été effacées." :
                  "تم مسح كافة التطبيقات المحظورة.";
        MessageBox.Show(msg, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadLimitFields()
    {
        if (User.DailyMinutes.HasValue)
        {
            DailyMinutes = User.DailyMinutes.Value;
            ScheduleStart = User.ScheduleStart ?? "08:00";
            ScheduleEnd = User.ScheduleEnd ?? "22:00";
        }
        else
        {
            DailyMinutes = 120;
            ScheduleStart = "08:00";
            ScheduleEnd = "22:00";
        }
    }
}
