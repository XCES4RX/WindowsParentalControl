using CommunityToolkit.Mvvm.ComponentModel;
using ParentalControl.Admin.Services;
using ParentalControl.Core.Data;
using ParentalControl.Core.Localization;
using ParentalControl.Core.Models;

namespace ParentalControl.Admin.ViewModels;

public partial class UserRow : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _sid = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private bool _isRestricted;

    [ObservableProperty]
    private bool _isAdmin;

    public string UserType => IsAdmin
        ? LocalizationManager.T("admin_role_text", LocalizedStrings.Instance.CurrentLanguage)
        : LocalizationManager.T("std_role_text", LocalizedStrings.Instance.CurrentLanguage);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LimitSummary))]
    private int? _dailyMinutes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScheduleSummary))]
    private string? _scheduleStart;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScheduleSummary))]
    private string? _scheduleEnd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LimitSummary))]
    private int _todayMinutesUsed;

    [ObservableProperty]
    private string _restrictionsSummary = string.Empty;

    public string LimitSummary
    {
        get
        {
            var lang = LocalizedStrings.Instance.CurrentLanguage;
            if (!DailyMinutes.HasValue)
            {
                return LocalizationManager.T("summary_no_limit", lang);
            }
            var total = DailyMinutes.Value;
            var remaining = Math.Max(0, total - TodayMinutesUsed);
            return LocalizationManager.T("summary_limit_format", lang, total, remaining);
        }
    }

    public string ScheduleSummary
    {
        get
        {
            var lang = LocalizedStrings.Instance.CurrentLanguage;
            if (ScheduleStart is not null && ScheduleEnd is not null)
            {
                return LocalizationManager.T("summary_sched_format", lang, ScheduleStart, ScheduleEnd);
            }
            return LocalizationManager.T("summary_no_sched", lang);
        }
    }

    public static UserRow ForAdmin(string sid, string username)
    {
        return new UserRow
        {
            Id = -1,
            Sid = sid,
            Username = username,
            IsRestricted = false,
            IsAdmin = true
        };
    }

    public static UserRow FromDatabase(User user)
    {
        var row = new UserRow
        {
            Id = user.Id,
            Sid = user.Sid,
            Username = user.Username,
            IsRestricted = user.IsRestricted,
            IsAdmin = false
        };

        var limit = LimitRepository.GetByUserId(user.Id);
        if (limit is not null)
        {
            row.DailyMinutes = limit.DailyMinutes;
            row.ScheduleStart = limit.ScheduleStart.ToString("HH:mm");
            row.ScheduleEnd = limit.ScheduleEnd.ToString("HH:mm");
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var usage = UsageRepository.GetUsage(user.Id, today);
        if (usage is not null)
        {
            row.TodayMinutesUsed = usage.MinutesUsed;
        }

        row.RefreshRestrictionsSummary();
        return row;
    }

    public void RefreshRestrictionsSummary()
    {
        var lang = LocalizedStrings.Instance.CurrentLanguage;
        var policy = UserPolicyRepository.GetByUserId(Id);
        if (policy is null)
        {
            RestrictionsSummary = LocalizationManager.T("summary_no_restrictions", lang);
            return;
        }

        var count = (policy.LockTaskbar ? 1 : 0)
                  + (policy.BlockSettings ? 1 : 0)
                  + (policy.BlockTaskManager ? 1 : 0)
                  + (policy.BlockCommandPrompt ? 1 : 0);

        RestrictionsSummary = count == 0
            ? LocalizationManager.T("summary_no_restrictions", lang)
            : LocalizationManager.T("summary_restrictions_count", lang, count);
    }

    public void RefreshLimits()
    {
        var limit = LimitRepository.GetByUserId(Id);
        if (limit is not null)
        {
            DailyMinutes = limit.DailyMinutes;
            ScheduleStart = limit.ScheduleStart.ToString("HH:mm");
            ScheduleEnd = limit.ScheduleEnd.ToString("HH:mm");
        }
        else
        {
            DailyMinutes = null;
            ScheduleStart = null;
            ScheduleEnd = null;
        }
    }

    public void RefreshUsage()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var usage = UsageRepository.GetUsage(Id, today);
        TodayMinutesUsed = usage?.MinutesUsed ?? 0;
    }
}
