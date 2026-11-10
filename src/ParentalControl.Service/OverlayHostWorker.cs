using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;
using ParentalControl.Core.Platform;

namespace ParentalControl.Service;

/// <summary>
/// Hosts the status Named Pipe for the child countdown overlay, launches the overlay in
/// active restricted sessions, enforces blocked applications, and captures screenshots.
/// </summary>
public sealed class OverlayHostWorker : BackgroundService
{
    private const string StatusPipeName = "ParentalControl_OverlayStatus";
    private const string OverlayProcessName = "ParentalControl.Overlay";

    private readonly SessionTracker _sessionTracker;
    private readonly TelegramNotifier _notifier;
    private readonly Serilog.ILogger _logger;
    private readonly ConcurrentDictionary<int, DateTime> _lastTimeRequests = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastAppBlockAlerts = new();

    public OverlayHostWorker(SessionTracker sessionTracker, TelegramNotifier notifier, Serilog.ILogger logger)
    {
        _sessionTracker = sessionTracker;
        _notifier = notifier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Overlay host worker started");

        var pipeTask = RunStatusPipeServerAsync(stoppingToken);
        var watchdogTask = RunOverlayWatchdogAsync(stoppingToken);

        await Task.WhenAll(pipeTask, watchdogTask);
    }

    private async Task RunStatusPipeServerAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pipeSecurity = CreateAuthenticatedUserPipeSecurity();
                await using var pipe = NamedPipeServerStreamAcl.Create(
                    StatusPipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    0,
                    0,
                    pipeSecurity);

                await pipe.WaitForConnectionAsync(stoppingToken);

                using var reader = new StreamReader(pipe, leaveOpen: true);
                using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

                var request = await reader.ReadLineAsync(stoppingToken);
                if (!string.IsNullOrWhiteSpace(request) &&
                    request.StartsWith("REQUEST_TIME:", StringComparison.OrdinalIgnoreCase))
                {
                    var ack = HandleTimeRequest(request);
                    await writer.WriteLineAsync(ack);
                    continue;
                }

                var sessionId = ParseSessionId(request);
                var status = BuildOverlayStatus(sessionId);
                var json = JsonSerializer.Serialize(status);

                await writer.WriteLineAsync(json);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error in overlay status pipe server");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task RunOverlayWatchdogAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                EnsureOverlaysRunning();
                EnforceBlockedApps();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error in overlay/app watchdog");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private string HandleTimeRequest(string request)
    {
        var parts = request.Split(':');
        if (parts.Length < 3 ||
            !int.TryParse(parts[1], out var sessionId) ||
            !int.TryParse(parts[2], out var minutes))
        {
            return "ERR";
        }

        if (!_sessionTracker.ActiveSessions.TryGetValue(sessionId, out var session))
            return "ERR";

        var now = DateTime.Now;
        if (_lastTimeRequests.TryGetValue(sessionId, out var last) && (now - last).TotalSeconds < 60)
            return "COOLDOWN";

        var user = UserRepository.GetBySid(session.UserSid);
        if (user is null) return "ERR";

        _lastTimeRequests[sessionId] = now;
        EventRepository.LogEvent(session.UserSid, EventType.TIME_REQUESTED, $"Requested +{minutes} min");
        _notifier.NotifyExtraTimeRequest(user.Id, session.Username, minutes);
        return "OK";
    }

    private void EnforceBlockedApps()
    {
        if (_sessionTracker.ActiveSessions.Count == 0) return;

        var sessionBlocked = new Dictionary<int, (string Username, string Sid, HashSet<string> Apps)>();
        foreach (var (sessionId, session) in _sessionTracker.ActiveSessions)
        {
            var user = UserRepository.GetBySid(session.UserSid);
            if (user is null) continue;
            var apps = BlockedAppRepository.GetByUserId(user.Id);
            if (apps.Count > 0)
            {
                sessionBlocked[sessionId] = (session.Username, session.UserSid, new HashSet<string>(apps, StringComparer.OrdinalIgnoreCase));
            }
        }

        if (sessionBlocked.Count == 0) return;

        foreach (var proc in Process.GetProcesses())
        {
            using (proc)
            {
                try
                {
                    if (!sessionBlocked.TryGetValue(proc.SessionId, out var info))
                        continue;

                    var procName = proc.ProcessName;
                    if (!info.Apps.Contains(procName))
                        continue;

                    proc.Kill(entireProcessTree: true);
                    _logger.Information("Blocked app {ProcessName} killed in session {SessionId} ({Username})",
                        procName, proc.SessionId, info.Username);

                    var key = $"{proc.SessionId}:{procName.ToLowerInvariant()}";
                    var now = DateTime.Now;
                    if (!_lastAppBlockAlerts.TryGetValue(key, out var lastAlert) || (now - lastAlert).TotalSeconds >= 60)
                    {
                        _lastAppBlockAlerts[key] = now;
                        EventRepository.LogEvent(info.Sid, EventType.APP_BLOCKED, $"Blocked {procName}.exe");
                        _notifier.NotifyAlert("تم حظر تشغيل تطبيق", info.Username, $"تم إغلاق {procName}.exe تلقائياً.");
                    }
                }
                catch
                {
                    // Process may have already exited
                }
            }
        }
    }

    public void EnsureOverlaysRunning()
    {
        var settings = SettingsRepository.Get();
        if (!settings.ShowCountdownOverlay) return;

        var overlayExe = ResolveOverlayExePath();
        if (overlayExe is null) return;

        foreach (var (sessionId, session) in _sessionTracker.ActiveSessions)
        {
            var user = UserRepository.GetBySid(session.UserSid);
            if (user is null || !user.IsRestricted) continue;

            var limit = LimitRepository.GetByUserId(user.Id);
            if (limit is null) continue;

            if (!SessionManager.IsProcessRunningInSession(OverlayProcessName, sessionId))
            {
                var launched = SessionManager.LaunchInSession(sessionId, overlayExe);
                if (launched)
                {
                    _logger.Debug("Launched countdown overlay for {Username} (session {SessionId})",
                        session.Username, sessionId);
                }
            }
        }
    }

    public async Task<(byte[] PngBytes, string Username, int SessionId)?> CaptureScreenshotAsync(CancellationToken ct = default)
    {
        var overlayExe = ResolveOverlayExePath();
        if (overlayExe is null)
            throw new FileNotFoundException("ParentalControl.Overlay.exe was not found.");

        var target = ResolveTargetSession();
        if (target is null) return null;

        var (sessionId, username) = target.Value;
        var pipeName = $"ParentalControl_Shot_{Guid.NewGuid():N}";
        var pipeSecurity = CreateAuthenticatedUserPipeSecurity();

        await using var pipe = NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.In,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            pipeSecurity);

        if (!SessionManager.LaunchInSession(sessionId, overlayExe, $"--screenshot-pipe {pipeName}", hidden: true))
            return null;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        await pipe.WaitForConnectionAsync(timeoutCts.Token);
        using var ms = new MemoryStream();
        await pipe.CopyToAsync(ms, timeoutCts.Token);

        var bytes = ms.ToArray();
        return bytes.Length > 0 ? (bytes, username, sessionId) : null;
    }

    private (int SessionId, string Username)? ResolveTargetSession()
    {
        var consoleId = SessionManager.GetActiveConsoleSessionId();
        if (consoleId.HasValue)
        {
            if (_sessionTracker.ActiveSessions.TryGetValue(consoleId.Value, out var consoleSession))
                return (consoleId.Value, consoleSession.Username);

            var consoleUser = SessionManager.GetSessionUsername(consoleId.Value);
            if (!string.IsNullOrWhiteSpace(consoleUser))
                return (consoleId.Value, consoleUser);
        }

        foreach (var (sessionId, session) in _sessionTracker.ActiveSessions)
        {
            return (sessionId, session.Username);
        }

        return null;
    }

    private OverlayStatus BuildOverlayStatus(int sessionId)
    {
        var settings = SettingsRepository.Get();
        if (!settings.ShowCountdownOverlay)
            return new OverlayStatus { ShouldShow = false };

        if (!_sessionTracker.ActiveSessions.TryGetValue(sessionId, out var session))
            return new OverlayStatus { ShouldShow = false };

        var user = UserRepository.GetBySid(session.UserSid);
        if (user is null || !user.IsRestricted)
            return new OverlayStatus { ShouldShow = false };

        var limit = LimitRepository.GetByUserId(user.Id);
        if (limit is null)
            return new OverlayStatus { ShouldShow = false };

        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var usage = UsageRepository.GetUsage(user.Id, today);
        var usedMinutes = usage?.MinutesUsed ?? 0;

        var remainingDailyMinutes = Math.Max(0, limit.DailyMinutes - usedMinutes);
        var secondsSinceLastTick = Math.Clamp((int)(now - session.LastTick).TotalSeconds, 0, 59);
        var dailySeconds = Math.Max(0, remainingDailyMinutes * 60 - secondsSinceLastTick);

        var scheduleEndDt = now.Date.Add(limit.ScheduleEnd.ToTimeSpan());
        var scheduleSeconds = Math.Max(0, (int)(scheduleEndDt - now).TotalSeconds);

        return new OverlayStatus
        {
            ShouldShow = true,
            Username = session.Username,
            IsRestricted = true,
            DailyMinutes = limit.DailyMinutes,
            MinutesUsed = usedMinutes,
            ScheduleStart = limit.ScheduleStart.ToString("HH:mm"),
            ScheduleEnd = limit.ScheduleEnd.ToString("HH:mm"),
            EffectiveSecondsRemaining = Math.Min(dailySeconds, scheduleSeconds)
        };
    }

    private static int ParseSessionId(string? request)
    {
        if (!string.IsNullOrWhiteSpace(request) &&
            request.StartsWith("STATUS:", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(request.AsSpan(7), out var sid))
        {
            return sid;
        }
        return -1;
    }

    private static string? ResolveOverlayExePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "overlay", "ParentalControl.Overlay.exe")),
            Path.Combine(baseDir, "ParentalControl.Overlay.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static PipeSecurity CreateAuthenticatedUserPipeSecurity()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        return security;
    }
}
