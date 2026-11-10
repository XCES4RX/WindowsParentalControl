using ParentalControl.Core.Data;
using ParentalControl.Core.Models;
using ParentalControl.Core.Platform;
namespace ParentalControl.Service;

public sealed class UsageMonitorWorker : BackgroundService
{
    private readonly SessionTracker _sessionTracker;
    private readonly TelegramNotifier _notifier;
    private readonly Serilog.ILogger _logger;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
    private DateOnly _lastCleanupDate = DateOnly.MinValue;
    private DateOnly _lastSummaryDate = DateOnly.MinValue;
    private readonly HashSet<(int SessionId, int Threshold)> _warnedThresholds = [];

    public UsageMonitorWorker(SessionTracker sessionTracker, TelegramNotifier notifier, Serilog.ILogger logger)
    {
        _sessionTracker = sessionTracker;
        _notifier = notifier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Usage monitor worker started");

        try
        {
            ProcessTick();
            RunCleanupIfNeeded();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during initial usage monitor tick");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TickInterval, stoppingToken);

            if (!_sessionTracker.IsAwake)
                continue;

            try
            {
                ProcessTick();
                RunCleanupIfNeeded();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during usage monitor tick");
            }
        }
    }

    private void ProcessTick()
    {
        _sessionTracker.TickAllSessions();

        var today = DateOnly.FromDateTime(DateTime.Now);
        var now = TimeOnly.FromDateTime(DateTime.Now);

        foreach (var (sessionId, session) in _sessionTracker.ActiveSessions)
        {
            var user = UserRepository.GetBySid(session.UserSid);
            if (user is null) continue;

            WindowsPolicyManager.ApplyUserPolicies(session.UserSid, user.Id);
            if (!user.IsRestricted) continue;

            var limit = LimitRepository.GetByUserId(user.Id);
            if (limit is null) continue;

            var usage = UsageRepository.GetUsage(user.Id, today);
            if (usage is not null && usage.MinutesUsed >= limit.DailyMinutes)
            {
                _logger.Information("Daily limit reached for {Username} ({MinutesUsed}/{DailyMinutes} min)",
                    session.Username, usage.MinutesUsed, limit.DailyMinutes);
                EventRepository.LogEvent(session.UserSid, EventType.LIMIT_REACHED,
                    $"Used {usage.MinutesUsed} of {limit.DailyMinutes} minutes");
                SessionManager.ForceLogoff(sessionId);
                EventRepository.LogEvent(session.UserSid, EventType.FORCED_LOGOUT, "Daily limit reached");
                _notifier.NotifyAlert("انتهى الوقت اليومي", session.Username,
                    $"استخدم {usage.MinutesUsed} من {limit.DailyMinutes} دقيقة — تم تسجيل الخروج.");
                _sessionTracker.RemoveSession(sessionId);
                continue;
            }

            if (now < limit.ScheduleStart || now >= limit.ScheduleEnd)
            {
                _logger.Information("Outside allowed schedule for {Username} (allowed {Start}-{End})",
                    session.Username, limit.ScheduleStart, limit.ScheduleEnd);
                SessionManager.ForceLogoff(sessionId);
                EventRepository.LogEvent(session.UserSid, EventType.FORCED_LOGOUT, "Outside allowed schedule");
                _notifier.NotifyAlert("خارج الوقت المسموح", session.Username,
                    $"الجدول المسموح {limit.ScheduleStart:HH:mm} - {limit.ScheduleEnd:HH:mm} — تم تسجيل الخروج.");
                _sessionTracker.RemoveSession(sessionId);
                continue;
            }

            CheckAndSendWarnings(sessionId, session.Username, limit, usage?.MinutesUsed ?? 0);
        }

        SendDailySummaryIfDue();
    }

    private void CheckAndSendWarnings(int sessionId, string username, LimitConfig limit, int usedMinutes)
    {
        var dailyLeft = Math.Max(0, limit.DailyMinutes - usedMinutes);
        var scheduleLeft = Math.Max(0, (int)Math.Ceiling((DateTime.Today.Add(limit.ScheduleEnd.ToTimeSpan()) - DateTime.Now).TotalMinutes));
        var effectiveLeft = Math.Min(dailyLeft, scheduleLeft);

        if (effectiveLeft > 5)
        {
            _warnedThresholds.Remove((sessionId, 5));
            _warnedThresholds.Remove((sessionId, 1));
            return;
        }

        if (effectiveLeft is <= 5 and > 1 && _warnedThresholds.Add((sessionId, 5)))
        {
            SessionManager.SendSessionMessage(
                sessionId,
                "Parental Control — Time Warning",
                $"Hi {username}, you have {effectiveLeft} minutes remaining before your session ends. Please save your work.");
        }
        else if (effectiveLeft <= 1 && _warnedThresholds.Add((sessionId, 1)))
        {
            SessionManager.SendSessionMessage(
                sessionId,
                "Parental Control — Final Warning",
                $"Hi {username}, less than 1 minute remaining! Your session will sign out very soon.");
        }
    }

    private void SendDailySummaryIfDue()
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        if (now.Hour < 21 || today <= _lastSummaryDate) return;

        _lastSummaryDate = today;
        var users = UserRepository.GetAll();
        if (users.Count == 0) return;

        var lines = new List<string> { $"📊 <b>التقرير اليومي — {Environment.MachineName} ({today:yyyy-MM-dd})</b>" };
        foreach (var u in users)
        {
            var limit = LimitRepository.GetByUserId(u.Id);
            var used = UsageRepository.GetUsage(u.Id, today)?.MinutesUsed ?? 0;
            lines.Add(limit is not null
                ? $"• <b>{u.Username}</b>: <code>{used}/{limit.DailyMinutes}</code> دقيقة"
                : $"• <b>{u.Username}</b>: <code>{used}</code> دقيقة (غير مقيّد)");
        }

        _ = _notifier.SendRawIfEnabledAsync(string.Join("\n", lines));
    }

    private void RunCleanupIfNeeded()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today <= _lastCleanupDate) return;

        _lastCleanupDate = today;

        var eventsCutoff = DateTime.Now.AddDays(-DatabaseManager.RetentionDays);
        var usageCutoff = today.AddDays(-DatabaseManager.RetentionDays);

        var eventsDeleted = EventRepository.DeleteOlderThan(eventsCutoff);
        var usageDeleted = UsageRepository.DeleteOlderThan(usageCutoff);

        if (eventsDeleted > 0 || usageDeleted > 0)
        {
            _logger.Information("Database cleanup: deleted {EventsDeleted} events and {UsageDeleted} usage records older than {Days} days",
                eventsDeleted, usageDeleted, DatabaseManager.RetentionDays);
        }
    }
}
