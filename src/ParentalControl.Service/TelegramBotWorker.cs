using System.Diagnostics;
using System.Text;
using ParentalControl.Core;
using ParentalControl.Core.Data;
using ParentalControl.Core.Localization;
using ParentalControl.Core.Models;
using ParentalControl.Core.Platform;
using ParentalControl.Core.Security;
using ParentalControl.Core.Telegram;

namespace ParentalControl.Service;

/// <summary>
/// Long-polling Telegram bot worker providing multilingual remote management,
/// offline queue catch-up, silent auto-updates, live screenshots, and session controls.
/// </summary>
public sealed class TelegramBotWorker : BackgroundService
{
    private readonly TelegramApiClient _client;
    private readonly SessionTracker _sessionTracker;
    private readonly OverlayHostWorker _overlayHost;
    private readonly AutoUpdateWorker _autoUpdateWorker;
    private readonly Serilog.ILogger _logger;
    private readonly DateTime _serviceStartedAt = DateTime.Now;
    private readonly Dictionary<string, (string Mode, int UserId)> _pendingInputs = new();
    private long _updateOffset;
    private bool _isFirstPoll = true;
    private DateOnly _lastDailyDigestDate = DateOnly.MinValue;

    private async Task CheckSendDailyDigestAsync(string token, string chatId, string lang, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(chatId)) return;
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        if (now.Hour >= 21 && _lastDailyDigestDate != today)
        {
            _lastDailyDigestDate = today;
            await SendTopActivityAsync(token, chatId, lang, ct);
        }
    }

    public TelegramBotWorker(
        TelegramApiClient client,
        SessionTracker sessionTracker,
        OverlayHostWorker overlayHost,
        AutoUpdateWorker autoUpdateWorker,
        Serilog.ILogger logger)
    {
        _client = client;
        _sessionTracker = sessionTracker;
        _overlayHost = overlayHost;
        _autoUpdateWorker = autoUpdateWorker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Telegram bot worker started");
        _updateOffset = SettingsRepository.GetTelegramUpdateOffset();

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = SettingsRepository.Get();
            if (!settings.TelegramEnabled || string.IsNullOrWhiteSpace(settings.TelegramBotToken))
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            try
            {
                var updates = await _client.GetUpdatesAsync(
                    settings.TelegramBotToken,
                    _updateOffset,
                    timeoutSeconds: 25,
                    ct: stoppingToken);

                var offlineAppliedChanges = new List<string>();

                foreach (var update in updates)
                {
                    _updateOffset = Math.Max(_updateOffset, update.UpdateId + 1);
                    SettingsRepository.SaveTelegramUpdateOffset(_updateOffset);

                    var changeDescription = await HandleUpdateAsync(settings, update, _isFirstPoll, stoppingToken);
                    if (_isFirstPoll && !string.IsNullOrWhiteSpace(changeDescription))
                    {
                        offlineAppliedChanges.Add(changeDescription);
                    }
                }

                if (_isFirstPoll)
                {
                    _isFirstPoll = false;
                    if (offlineAppliedChanges.Count > 0 && !string.IsNullOrWhiteSpace(settings.TelegramChatId))
                    {
                        var lang = settings.TelegramLanguage;
                        var sb = new StringBuilder();
                        sb.AppendLine(LocalizationManager.T("offline_applied_header", lang));
                        foreach (var change in offlineAppliedChanges)
                        {
                            sb.Append(LocalizationManager.T("offline_cmd_item", lang, change));
                        }
                        await _client.SendMessageAsync(
                            settings.TelegramBotToken,
                            settings.TelegramChatId,
                            sb.ToString(),
                            BuildMainKeyboard(lang),
                            stoppingToken);
                    }
                }

                await CheckSendDailyDigestAsync(settings.TelegramBotToken, settings.TelegramChatId, settings.TelegramLanguage, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Telegram polling error; retrying in 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<string?> HandleUpdateAsync(AppSettings settings, TelegramUpdate update, bool isOfflineCatchup, CancellationToken ct)
    {
        var lang = settings.TelegramLanguage;

        // Auto-pair if Chat ID is not yet set and parent sends /start or /pair
        if (string.IsNullOrWhiteSpace(settings.TelegramChatId))
        {
            var text = update.MessageText?.Trim() ?? string.Empty;
            if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("/pair", StringComparison.OrdinalIgnoreCase))
            {
                settings.TelegramChatId = update.ChatId;
                SettingsRepository.Save(settings);
                await SendMainMenuAsync(
                    settings.TelegramBotToken,
                    update.ChatId,
                    LocalizationManager.T("bot_paired", lang, Escape(Environment.MachineName), Escape(update.ChatId)),
                    lang,
                    ct);
            }
            else
            {
                await _client.SendMessageAsync(
                    settings.TelegramBotToken,
                    update.ChatId,
                    LocalizationManager.T("bot_help", lang, Escape(update.ChatId)),
                    ct: ct);
            }
            return null;
        }

        if (!string.Equals(update.ChatId, settings.TelegramChatId, StringComparison.Ordinal))
        {
            _logger.Warning("Ignored unauthorized Telegram message from chat {ChatId}", update.ChatId);
            return null;
        }

        if (!string.IsNullOrEmpty(update.CallbackQueryId))
        {
            _pendingInputs.Remove(update.ChatId);
            await _client.AnswerCallbackQueryAsync(settings.TelegramBotToken, update.CallbackQueryId, ct: ct);
            return await DispatchCommandAsync(settings.TelegramBotToken, update.ChatId, update.CallbackData ?? string.Empty, lang, ct);
        }

        if (!string.IsNullOrWhiteSpace(update.MessageText))
        {
            var rawText = update.MessageText.Trim();
            if (rawText.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                _pendingInputs.Remove(update.ChatId);
                await SendMainMenuAsync(settings.TelegramBotToken, update.ChatId, "Cancelled / تم الإلغاء.", lang, ct);
                return null;
            }

            if (!rawText.StartsWith('/') && _pendingInputs.TryGetValue(update.ChatId, out var pending))
            {
                return await HandlePendingInputAsync(settings.TelegramBotToken, update.ChatId, pending.Mode, pending.UserId, rawText, lang, ct);
            }

            _pendingInputs.Remove(update.ChatId);
            return await DispatchTextCommandAsync(settings.TelegramBotToken, update.ChatId, rawText, lang, ct);
        }

        return null;
    }

    private async Task<string?> DispatchTextCommandAsync(string token, string chatId, string rawText, string lang, CancellationToken ct)
    {
        var parts = rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();

        if (cmd is "/addtime" or "/settime" or "/setschedule")
        {
            return await HandleTextTimeCommandAsync(token, chatId, cmd, parts, lang, ct);
        }

        if (cmd is "/block" or "/unblock")
        {
            return await HandleTextBlockAppCommandAsync(token, chatId, cmd, parts, lang, ct);
        }

        if (cmd == "/msg")
        {
            return await HandleMsgCommandAsync(token, chatId, rawText, lang, ct);
        }

        if (cmd is "/pause" or "/resume")
        {
            return await HandlePauseResumeCommandAsync(token, chatId, cmd, parts, lang, ct);
        }

        if (cmd == "/dns")
        {
            return await HandleDnsCommandAsync(token, chatId, parts, lang, ct);
        }

        return await DispatchCommandAsync(token, chatId, cmd, lang, ct);
    }

    private async Task<string?> DispatchCommandAsync(string token, string chatId, string command, string lang, CancellationToken ct)
    {
        switch (command)
        {
            case "/start":
            case "/menu":
            case "/help":
            case "cmd:menu":
                await SendMainMenuAsync(token, chatId,
                    LocalizationManager.T("menu_title", lang, Escape(Environment.MachineName)), lang, ct);
                return null;

            case "/status":
            case "cmd:status":
                await SendStatusAsync(token, chatId, lang, ct);
                return null;

            case "/screenshot":
            case "cmd:screenshot":
                await SendScreenshotAsync(token, chatId, lang, ct);
                return null;

            case "/limits":
            case "cmd:limits":
                await SendLimitsAsync(token, chatId, lang, ct);
                return null;

            case "/policies":
            case "cmd:policies":
                await SendPoliciesAsync(token, chatId, lang, ct);
                return null;

            case "/events":
            case "cmd:events":
                await SendEventsAsync(token, chatId, lang, ct);
                return null;

            case "/language":
            case "/lang":
            case "cmd:language":
                await SendLanguageMenuAsync(token, chatId, ct);
                return null;

            case "/update":
            case "cmd:update":
                await HandleUpdateCommandAsync(token, chatId, lang, ct);
                return null;

            case "/top":
            case "/activity":
            case "cmd:top":
                await SendTopActivityAsync(token, chatId, lang, ct);
                return null;

            case "/pause":
            case "cmd:pause":
                await HandleQuickDinnerPauseAsync(token, chatId, lang, ct);
                return null;

            case "/dns":
            case "cmd:dns":
                await HandleDnsCommandAsync(token, chatId, Array.Empty<string>(), lang, ct);
                return null;

            default:
                if (command.StartsWith("set_lang:", StringComparison.OrdinalIgnoreCase))
                {
                    var chosenLang = command["set_lang:".Length..];
                    SettingsRepository.SaveTelegramLanguage(chosenLang);
                    await _client.SendMessageAsync(token, chatId,
                        LocalizationManager.T("lang_selected", chosenLang),
                        BuildMainKeyboard(chosenLang),
                        ct);
                    return null;
                }
                else if (TryParseTwoPartInt(command, "time_menu:", out var menuUserId))
                {
                    await SendUserTimeMenuAsync(token, chatId, menuUserId, lang, ct);
                    return null;
                }
                else if (TryParseThreePartInts(command, "adj_limit:", out var adjUserId, out var deltaMinutes))
                {
                    return await AdjustDailyLimitAsync(token, chatId, adjUserId, deltaMinutes, lang, ct);
                }
                else if (TryParseTwoPartInt(command, "reset_usage:", out var resetUserId))
                {
                    return await ResetTodayUsageAsync(token, chatId, resetUserId, lang, ct);
                }
                else if (TryParseTwoPartInt(command, "remove_limit:", out var removeUserId))
                {
                    return await RemoveUserLimitAsync(token, chatId, removeUserId, lang, ct);
                }
                else if (TryParseTwoPartInt(command, "prompt_limit:", out var promptLimitUserId))
                {
                    _pendingInputs[chatId] = ("limit", promptLimitUserId);
                    await _client.SendMessageAsync(token, chatId,
                        lang == "en" ? "✏️ Send new daily limit in minutes (e.g. <code>90</code> or <code>120</code>), or /cancel." :
                        lang == "fr" ? "✏️ Envoyez la nouvelle limite quotidienne en minutes (ex. <code>90</code> ou <code>120</code>), ou /cancel." :
                        "✏️ أرسل الحد اليومي الجديد بالدقائق (مثال: <code>90</code> أو <code>120</code>)، أو أرسل /cancel للإلغاء.", ct: ct);
                    return null;
                }
                else if (TryParseTwoPartInt(command, "prompt_usage:", out var promptUsageUserId))
                {
                    _pendingInputs[chatId] = ("usage", promptUsageUserId);
                    await _client.SendMessageAsync(token, chatId,
                        lang == "en" ? "📝 Send used minutes today (e.g. <code>0</code> or <code>30</code>), or /cancel." :
                        lang == "fr" ? "📝 Envoyez les minutes utilisées aujourd'hui (ex. <code>0</code> ou <code>30</code>), ou /cancel." :
                        "📝 أرسل عدد الدقائق المستخدمة اليوم (مثال: <code>0</code> أو <code>30</code>)، أو أرسل /cancel للإلغاء.", ct: ct);
                    return null;
                }
                else if (TryParseTwoPartInt(command, "prompt_schedule:", out var promptSchedUserId))
                {
                    _pendingInputs[chatId] = ("schedule", promptSchedUserId);
                    await _client.SendMessageAsync(token, chatId,
                        lang == "en" ? "🕒 Send allowed schedule as <code>HH:mm-HH:mm</code> (e.g. <code>08:00-21:30</code>), or /cancel." :
                        lang == "fr" ? "🕒 Envoyez la plage horaire autorisée sous la forme <code>HH:mm-HH:mm</code> (ex. <code>08:00-21:30</code>), ou /cancel." :
                        "🕒 أرسل الجدول الزمني المسموح بصيغة <code>HH:mm-HH:mm</code> (مثال: <code>08:00-21:30</code>)، أو أرسل /cancel للإلغاء.", ct: ct);
                    return null;
                }
                else if (TryParseThreePartInts(command, "approve_time:", out var approveUserId, out var approveMin))
                {
                    return await AdjustDailyLimitAsync(token, chatId, approveUserId, approveMin, lang, ct);
                }
                else if (TryParseTwoPartInt(command, "deny_time:", out var denyUserId))
                {
                    var deniedUser = UserRepository.GetAll().FirstOrDefault(u => u.Id == denyUserId);
                    var uName = deniedUser?.Username ?? denyUserId.ToString();
                    var msg = lang == "en" ? $"❌ Extra time request denied for <b>{Escape(uName)}</b>." :
                              lang == "fr" ? $"❌ Demande de temps refusée pour <b>{Escape(uName)}</b>." :
                              $"❌ تم رفض طلب الوقت الإضافي للمستخدم <b>{Escape(uName)}</b>.";
                    await _client.SendMessageAsync(token, chatId, msg, BuildMainKeyboard(lang), ct);
                    return $"Denied extra time for {uName}";
                }
                else if (TryParseTwoPartInt(command, "lock_screen:", out var lockSessionId))
                {
                    return await RemoteLockScreenAsync(token, chatId, lockSessionId, lang, ct);
                }
                else if (TryParseTwoPartInt(command, "logoff:", out var logoffSessionId))
                {
                    return await RemoteLogoffAsync(token, chatId, logoffSessionId, lang, ct);
                }
                else
                {
                    await SendMainMenuAsync(token, chatId, LocalizationManager.T("menu_title", lang, Escape(Environment.MachineName)), lang, ct);
                    return null;
                }
        }
    }

    private async Task SendLanguageMenuAsync(string token, string chatId, CancellationToken ct)
    {
        var text = "🌐 <b>اختر لغة البوت / Choose Bot Language / Choisissez la langue du bot:</b>";
        var keyboard = new
        {
            inline_keyboard = new object[][]
            {
                [
                    new { text = "🇸🇦 العربية", callback_data = "set_lang:ar" },
                    new { text = "🇬🇧 English", callback_data = "set_lang:en" },
                    new { text = "🇫🇷 Français", callback_data = "set_lang:fr" }
                ],
                [
                    new { text = "🏠 Main Menu / القائمة الرئيسية", callback_data = "cmd:menu" }
                ]
            }
        };
        await _client.SendMessageAsync(token, chatId, text, keyboard, ct);
    }

    private async Task HandleUpdateCommandAsync(string token, string chatId, string lang, CancellationToken ct)
    {
        await _client.SendMessageAsync(token, chatId,
            lang == "en" ? "🔄 Checking for latest GitHub release..." :
            lang == "fr" ? "🔄 Vérification des mises à jour sur GitHub..." :
            "🔄 جاري فحص أحدث إصدار على غيت هب...",
            ct: ct);

        var (hasUpdate, versionTag, msg) = await _autoUpdateWorker.CheckOnlyAsync(ct);
        if (hasUpdate)
        {
            await _client.SendMessageAsync(token, chatId,
                LocalizationManager.T("update_available", lang, versionTag),
                BuildMainKeyboard(lang),
                ct);
            _ = _autoUpdateWorker.CheckAndApplyUpdateAsync(manual: true, ct: CancellationToken.None);
        }
        else
        {
            var text = string.IsNullOrEmpty(versionTag)
                ? LocalizationManager.T("update_check_failed", lang, msg)
                : LocalizationManager.T("update_no_new", lang, AppVersionInfo.VersionString);
            await _client.SendMessageAsync(token, chatId, text, BuildMainKeyboard(lang), ct);
        }
    }

    private async Task SendMainMenuAsync(string token, string chatId, string headerText, string lang, CancellationToken ct)
    {
        await _client.SendMessageAsync(token, chatId, headerText, BuildMainKeyboard(lang), ct);
    }

    private async Task SendStatusAsync(string token, string chatId, string lang, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var serviceUptime = DateTime.Now - _serviceStartedAt;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var now = DateTime.Now;

        sb.AppendLine(LocalizationManager.T("status_header", lang,
            Escape(Environment.MachineName), FormatSpan(uptime), FormatSpan(serviceUptime)));

        var actionRows = new List<object[]>();

        if (_sessionTracker.ActiveSessions.Count == 0)
        {
            sb.AppendLine(LocalizationManager.T("status_no_active", lang));
        }
        else
        {
            sb.AppendLine(LocalizationManager.T("status_active_users", lang));
            foreach (var (sessionId, session) in _sessionTracker.ActiveSessions)
            {
                var user = UserRepository.GetBySid(session.UserSid);
                var limit = user is not null ? LimitRepository.GetByUserId(user.Id) : null;
                var usage = user is not null ? UsageRepository.GetUsage(user.Id, today) : null;
                var used = usage?.MinutesUsed ?? 0;

                if (user is not null && user.IsRestricted && limit is not null)
                {
                    var dailyLeftMin = Math.Max(0, limit.DailyMinutes - used);
                    var schedEndDt = now.Date.Add(limit.ScheduleEnd.ToTimeSpan());
                    var schedLeftMin = Math.Max(0, (int)(schedEndDt - now).TotalMinutes);
                    var effectiveLeft = Math.Min(dailyLeftMin, schedLeftMin);

                    sb.AppendLine(LocalizationManager.T("status_user_line", lang,
                        Escape(session.Username), sessionId,
                        LocalizationManager.T("status_restricted", lang),
                        used, limit.DailyMinutes, effectiveLeft, limit.ScheduleEnd.ToString("HH:mm")));

                    actionRows.Add(
                    [
                        new { text = LocalizationManager.T("btn_adjust_time", lang, session.Username), callback_data = $"time_menu:{user.Id}" },
                        new { text = LocalizationManager.T("btn_lock", lang, session.Username), callback_data = $"lock_screen:{sessionId}" },
                        new { text = LocalizationManager.T("btn_logout", lang, session.Username), callback_data = $"logoff:{sessionId}" }
                    ]);
                }
                else
                {
                    sb.AppendLine(LocalizationManager.T("status_user_line", lang,
                        Escape(session.Username), sessionId,
                        LocalizationManager.T("status_unrestricted", lang),
                        used, "∞", "∞", "23:59"));

                    var rowButtons = new List<object>();
                    if (user is not null)
                    {
                        rowButtons.Add(new { text = LocalizationManager.T("btn_adjust_time", lang, session.Username), callback_data = $"time_menu:{user.Id}" });
                    }
                    rowButtons.Add(new { text = LocalizationManager.T("btn_lock", lang, session.Username), callback_data = $"lock_screen:{sessionId}" });
                    rowButtons.Add(new { text = LocalizationManager.T("btn_logout", lang, session.Username), callback_data = $"logoff:{sessionId}" });
                    actionRows.Add(rowButtons.ToArray());
                }
            }
        }

        actionRows.Add(
        [
            new { text = LocalizationManager.T("btn_screenshot", lang), callback_data = "cmd:screenshot" },
            new { text = LocalizationManager.T("btn_refresh", lang), callback_data = "cmd:status" }
        ]);
        actionRows.Add([new { text = LocalizationManager.T("btn_main_menu", lang), callback_data = "cmd:menu" }]);

        await _client.SendMessageAsync(token, chatId, sb.ToString(), new { inline_keyboard = actionRows }, ct);
    }

    private async Task SendScreenshotAsync(string token, string chatId, string lang, CancellationToken ct)
    {
        await _client.SendMessageAsync(token, chatId,
            lang == "en" ? "📸 Capturing live desktop screenshot..." :
            lang == "fr" ? "📸 Capture d'écran en cours..." :
            "📸 جاري التقاط لقطة شاشة...", ct: ct);

        var shot = await _overlayHost.CaptureScreenshotAsync(ct);
        if (shot is null)
        {
            await _client.SendMessageAsync(
                token,
                chatId,
                LocalizationManager.T("photo_no_session", lang),
                BuildMainKeyboard(lang),
                ct);
            return;
        }

        var (bytes, username, _) = shot.Value;
        var caption = LocalizationManager.T("photo_caption", lang,
            Environment.MachineName, DateTime.Now.ToString("HH:mm:ss"), username);
        await _client.SendPhotoAsync(token, chatId, bytes, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png", caption, ct);
    }

    private async Task SendLimitsAsync(string token, string chatId, string lang, CancellationToken ct)
    {
        var users = UserRepository.GetAll();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var sb = new StringBuilder();
        sb.AppendLine($"⏱ <b>{LocalizationManager.T("btn_time_limits", lang)} — {Escape(Environment.MachineName)}</b>\n");

        if (users.Count == 0)
        {
            sb.AppendLine("No standard user accounts found / لا توجد حسابات قياسية.");
        }
        else
        {
            foreach (var u in users)
            {
                var limit = LimitRepository.GetByUserId(u.Id);
                var usage = UsageRepository.GetUsage(u.Id, today);
                var used = usage?.MinutesUsed ?? 0;

                sb.AppendLine($"👤 <b>{Escape(u.Username)}</b> {(u.IsRestricted ? "🔒" : "🔓")}");
                if (limit is not null)
                {
                    var remaining = Math.Max(0, limit.DailyMinutes - used);
                    sb.AppendLine($"  • Used / المستهلك: <code>{used}/{limit.DailyMinutes}</code> m");
                    sb.AppendLine($"  • Remaining / المتبقي: <code>{FormatMinutes(remaining)}</code>");
                    sb.AppendLine($"  • Schedule / الأوقات: <code>{limit.ScheduleStart:HH:mm} - {limit.ScheduleEnd:HH:mm}</code>");
                }
                else
                {
                    sb.AppendLine($"  • No limits / بلا قيود (Used: <code>{used}</code> m)");
                }
                sb.AppendLine();
            }
        }

        var keyboardRows = new List<object[]>();
        foreach (var u in users)
        {
            keyboardRows.Add([new { text = $"⚙️ {u.Username} (+ / - / ...)", callback_data = $"time_menu:{u.Id}" }]);
        }
        keyboardRows.Add(
        [
            new { text = LocalizationManager.T("btn_refresh", lang), callback_data = "cmd:limits" },
            new { text = LocalizationManager.T("btn_main_menu", lang), callback_data = "cmd:menu" }
        ]);

        await _client.SendMessageAsync(token, chatId, sb.ToString(), new { inline_keyboard = keyboardRows }, ct);
    }

    private async Task SendPoliciesAsync(string token, string chatId, string lang, CancellationToken ct)
    {
        var users = UserRepository.GetAll();
        var sb = new StringBuilder();
        sb.AppendLine($"⚙️ <b>{LocalizationManager.T("btn_win_restrictions", lang)} — {Escape(Environment.MachineName)}</b>\n");

        if (users.Count == 0)
        {
            sb.AppendLine("No standard accounts found / لا توجد حسابات قياسية.");
        }
        else
        {
            foreach (var u in users)
            {
                var p = UserPolicyRepository.GetByUserId(u.Id);
                var apps = BlockedAppRepository.GetByUserId(u.Id);
                sb.AppendLine($"👤 <b>{Escape(u.Username)}</b>");
                sb.AppendLine($"  {(p?.LockTaskbar == true ? "✅" : "⬜")} Lock Taskbar / قفل شريط المهام");
                sb.AppendLine($"  {(p?.BlockSettings == true ? "✅" : "⬜")} Block Settings & Control Panel / منع الإعدادات");
                sb.AppendLine($"  {(p?.BlockTaskManager == true ? "✅" : "⬜")} Block Task Manager / منع مدير المهام");
                sb.AppendLine($"  {(p?.BlockCommandPrompt == true ? "✅" : "⬜")} Block Command Prompt / منع موجه الأوامر");
                sb.AppendLine(apps.Count > 0
                    ? $"  🚫 Blocked Apps / البرامج المحظورة: <code>{Escape(string.Join(", ", apps))}</code>"
                    : "  🚫 Blocked Apps / البرامج المحظورة: None / لا يوجد");
                sb.AppendLine();
            }
        }

        await _client.SendMessageAsync(token, chatId, sb.ToString(), BuildMainKeyboard(lang), ct);
    }

    private async Task SendEventsAsync(string token, string chatId, string lang, CancellationToken ct)
    {
        var events = EventRepository.GetEvents().Take(10).ToList();
        var usersBySid = UserRepository.GetAll().ToDictionary(u => u.Sid, u => u.Username);

        var sb = new StringBuilder();
        sb.AppendLine($"📋 <b>{LocalizationManager.T("btn_events", lang)} — {Escape(Environment.MachineName)}</b>\n");

        if (events.Count == 0)
        {
            sb.AppendLine("No logged events yet / لا توجد أحداث مسجلة بعد.");
        }
        else
        {
            foreach (var ev in events)
            {
                var name = usersBySid.GetValueOrDefault(ev.UserSid, ev.UserSid);
                var details = string.IsNullOrWhiteSpace(ev.Details) ? "" : $" — {Escape(ev.Details)}";
                sb.AppendLine($"• <code>{ev.Timestamp:MM-dd HH:mm}</code> | <b>{Escape(name)}</b> | <code>{ev.EventType}</code>{details}");
            }
        }

        await _client.SendMessageAsync(token, chatId, sb.ToString(), BuildMainKeyboard(lang), ct);
    }

    private async Task SendUserTimeMenuAsync(string token, string chatId, int userId, string lang, CancellationToken ct, string? notice = null)
    {
        var user = UserRepository.GetAll().FirstOrDefault(u => u.Id == userId);
        if (user is null)
        {
            await _client.SendMessageAsync(token, chatId, "User not found / المستخدم غير موجود.", BuildMainKeyboard(lang), ct);
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var limit = LimitRepository.GetByUserId(userId);
        var usage = UsageRepository.GetUsage(userId, today);
        var used = usage?.MinutesUsed ?? 0;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(notice))
        {
            sb.AppendLine(notice);
            sb.AppendLine();
        }

        sb.AppendLine($"⏱ <b>User Time: {Escape(user.Username)}</b>");
        sb.AppendLine($"• Status: {(user.IsRestricted ? "🔒 Restricted" : "🔓 Unrestricted")}");
        if (limit is not null)
        {
            var remaining = Math.Max(0, limit.DailyMinutes - used);
            sb.AppendLine($"• Daily Limit: <code>{limit.DailyMinutes}</code> min ({FormatMinutes(limit.DailyMinutes)})");
            sb.AppendLine($"• Used Today: <code>{used}</code> min");
            sb.AppendLine($"• Remaining: <code>{FormatMinutes(remaining)}</code>");
            sb.AppendLine($"• Schedule: <code>{limit.ScheduleStart:HH:mm} - {limit.ScheduleEnd:HH:mm}</code>");
        }
        else
        {
            sb.AppendLine($"• No daily limit set (Used Today: <code>{used}</code> min)");
        }

        var keyboard = new
        {
            inline_keyboard = new object[][]
            {
                [
                    new { text = "➕ +15m", callback_data = $"adj_limit:{userId}:15" },
                    new { text = "➕ +30m", callback_data = $"adj_limit:{userId}:30" },
                    new { text = "➕ +60m", callback_data = $"adj_limit:{userId}:60" }
                ],
                [
                    new { text = "➖ -15m", callback_data = $"adj_limit:{userId}:-15" },
                    new { text = "➖ -30m", callback_data = $"adj_limit:{userId}:-30" },
                    new { text = "➖ -60m", callback_data = $"adj_limit:{userId}:-60" }
                ],
                [
                    new { text = "✏️ Set Limit (min)", callback_data = $"prompt_limit:{userId}" },
                    new { text = "🕒 Schedule", callback_data = $"prompt_schedule:{userId}" }
                ],
                [
                    new { text = "📝 Edit Used (min)", callback_data = $"prompt_usage:{userId}" },
                    new { text = "🔄 Reset Today", callback_data = $"reset_usage:{userId}" }
                ],
                [
                    new { text = "🔓 Remove Limit", callback_data = $"remove_limit:{userId}" },
                    new { text = "⬅️ Back", callback_data = "cmd:limits" }
                ]
            }
        };

        await _client.SendMessageAsync(token, chatId, sb.ToString(), keyboard, ct);
    }

    private async Task<string> AdjustDailyLimitAsync(string token, string chatId, int userId, int deltaMinutes, string lang, CancellationToken ct)
    {
        var user = UserRepository.GetAll().FirstOrDefault(u => u.Id == userId);
        var uName = user?.Username ?? userId.ToString();
        var limit = EnsureOrGetLimit(userId);
        limit.DailyMinutes = Math.Clamp(limit.DailyMinutes + deltaMinutes, 1, 1440);
        LimitRepository.Upsert(limit);
        UserRepository.SetRestricted(userId, true);

        var sign = deltaMinutes >= 0 ? $"+{deltaMinutes}" : deltaMinutes.ToString();
        var feedback = LocalizationManager.T(deltaMinutes >= 0 ? "cmd_time_added" : "cmd_time_subtracted",
            lang, Math.Abs(deltaMinutes), uName);

        await SendUserTimeMenuAsync(token, chatId, userId, lang, ct, $"✅ {feedback}");
        return $"{sign} min for {uName}";
    }

    private async Task<string> ResetTodayUsageAsync(string token, string chatId, int userId, string lang, CancellationToken ct)
    {
        var user = UserRepository.GetAll().FirstOrDefault(u => u.Id == userId);
        var uName = user?.Username ?? userId.ToString();
        UsageRepository.SetUsage(userId, DateOnly.FromDateTime(DateTime.Now), 0);
        await SendUserTimeMenuAsync(token, chatId, userId, lang, ct, $"✅ Reset today's usage to 0 for {uName}.");
        return $"Reset usage for {uName}";
    }

    private async Task<string> RemoveUserLimitAsync(string token, string chatId, int userId, string lang, CancellationToken ct)
    {
        var user = UserRepository.GetAll().FirstOrDefault(u => u.Id == userId);
        var uName = user?.Username ?? userId.ToString();
        LimitRepository.Delete(userId);
        UserRepository.SetRestricted(userId, false);
        await SendUserTimeMenuAsync(token, chatId, userId, lang, ct, $"🔓 Removed limit for {uName}.");
        return $"Removed limit for {uName}";
    }

    private async Task<string?> HandlePendingInputAsync(
        string token,
        string chatId,
        string mode,
        int userId,
        string rawText,
        string lang,
        CancellationToken ct)
    {
        var user = UserRepository.GetAll().FirstOrDefault(u => u.Id == userId);
        var uName = user?.Username ?? userId.ToString();

        if (mode == "limit")
        {
            if (!int.TryParse(rawText, out var minutes) || minutes < 1 || minutes > 1440)
            {
                await _client.SendMessageAsync(token, chatId,
                    "⚠️ Enter a number between 1 and 1440 minutes (or /cancel).", ct: ct);
                return null;
            }

            _pendingInputs.Remove(chatId);
            var limit = EnsureOrGetLimit(userId);
            limit.DailyMinutes = minutes;
            LimitRepository.Upsert(limit);
            UserRepository.SetRestricted(userId, true);
            var msg = LocalizationManager.T("cmd_time_set", lang, uName, minutes);
            await SendUserTimeMenuAsync(token, chatId, userId, lang, ct, $"✅ {msg}");
            return $"Set limit to {minutes}m for {uName}";
        }

        if (mode == "usage")
        {
            if (!int.TryParse(rawText, out var usedMinutes) || usedMinutes < 0 || usedMinutes > 1440)
            {
                await _client.SendMessageAsync(token, chatId,
                    "⚠️ Enter a number between 0 and 1440 minutes (or /cancel).", ct: ct);
                return null;
            }

            _pendingInputs.Remove(chatId);
            UsageRepository.SetUsage(userId, DateOnly.FromDateTime(DateTime.Now), usedMinutes);
            await SendUserTimeMenuAsync(token, chatId, userId, lang, ct,
                $"✅ Set used minutes to {usedMinutes} for {uName}.");
            return $"Set usage to {usedMinutes}m for {uName}";
        }

        if (mode == "schedule")
        {
            if (!TryParseScheduleRange(rawText, out var start, out var end))
            {
                await _client.SendMessageAsync(token, chatId,
                    "⚠️ Invalid format. Use <code>HH:mm-HH:mm</code> (e.g. <code>08:00-21:30</code>) or /cancel.", ct: ct);
                return null;
            }

            _pendingInputs.Remove(chatId);
            var limit = EnsureOrGetLimit(userId);
            limit.ScheduleStart = start;
            limit.ScheduleEnd = end;
            LimitRepository.Upsert(limit);
            UserRepository.SetRestricted(userId, true);
            await SendUserTimeMenuAsync(token, chatId, userId, lang, ct,
                $"✅ Allowed schedule updated to <code>{start:HH:mm} - {end:HH:mm}</code>.");
            return $"Set schedule {start:HH:mm}-{end:HH:mm} for {uName}";
        }

        return null;
    }

    private async Task<string?> HandleTextTimeCommandAsync(
        string token,
        string chatId,
        string cmd,
        string[] parts,
        string lang,
        CancellationToken ct)
    {
        if (parts.Length < 3)
        {
            await _client.SendMessageAsync(token, chatId,
                "ℹ️ Syntax / الصيغة:\n<code>/addtime PC 30</code>\n<code>/settime PC 120</code>\n<code>/setschedule PC 08:00-21:30</code>",
                BuildMainKeyboard(lang), ct);
            return null;
        }

        var username = parts[1];
        var valueArg = parts[2];
        var user = UserRepository.GetAll()
            .FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            await _client.SendMessageAsync(token, chatId,
                $"⚠️ User <code>{Escape(username)}</code> not found.", BuildMainKeyboard(lang), ct);
            return null;
        }

        if (cmd == "/addtime" && int.TryParse(valueArg, out var delta))
        {
            return await AdjustDailyLimitAsync(token, chatId, user.Id, delta, lang, ct);
        }
        else if (cmd == "/settime" && int.TryParse(valueArg, out var minutes) && minutes is >= 1 and <= 1440)
        {
            var limit = EnsureOrGetLimit(user.Id);
            limit.DailyMinutes = minutes;
            LimitRepository.Upsert(limit);
            UserRepository.SetRestricted(user.Id, true);
            var msg = LocalizationManager.T("cmd_time_set", lang, user.Username, minutes);
            await SendUserTimeMenuAsync(token, chatId, user.Id, lang, ct, $"✅ {msg}");
            return $"Set limit to {minutes}m for {user.Username}";
        }
        else if (cmd == "/setschedule" && TryParseScheduleRange(valueArg, out var start, out var end))
        {
            var limit = EnsureOrGetLimit(user.Id);
            limit.ScheduleStart = start;
            limit.ScheduleEnd = end;
            LimitRepository.Upsert(limit);
            UserRepository.SetRestricted(user.Id, true);
            await SendUserTimeMenuAsync(token, chatId, user.Id, lang, ct,
                $"✅ Schedule updated to <code>{start:HH:mm} - {end:HH:mm}</code>.");
            return $"Set schedule {start:HH:mm}-{end:HH:mm} for {user.Username}";
        }
        else
        {
            await _client.SendMessageAsync(token, chatId, "⚠️ Invalid value / قيمة غير صالحة للأمر.", BuildMainKeyboard(lang), ct);
            return null;
        }
    }

    private async Task<string?> HandleTextBlockAppCommandAsync(
        string token,
        string chatId,
        string cmd,
        string[] parts,
        string lang,
        CancellationToken ct)
    {
        if (parts.Length < 3)
        {
            await _client.SendMessageAsync(token, chatId,
                "ℹ️ Syntax / الصيغة:\n<code>/block PC robloxplayerbeta</code>\n<code>/unblock PC robloxplayerbeta</code>",
                BuildMainKeyboard(lang), ct);
            return null;
        }

        var username = parts[1];
        var appName = parts[2];
        var user = UserRepository.GetAll()
            .FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            await _client.SendMessageAsync(token, chatId,
                $"⚠️ User <code>{Escape(username)}</code> not found.", BuildMainKeyboard(lang), ct);
            return null;
        }

        var norm = BlockedAppRepository.NormalizeProcessName(appName);
        if (string.IsNullOrEmpty(norm))
        {
            await _client.SendMessageAsync(token, chatId, "⚠️ Invalid application name.", BuildMainKeyboard(lang), ct);
            return null;
        }

        if (cmd == "/block")
        {
            BlockedAppRepository.Add(user.Id, norm);
            var msg = LocalizationManager.T("cmd_app_blocked", lang, norm, user.Username);
            await _client.SendMessageAsync(token, chatId, $"🚫 {msg}", BuildMainKeyboard(lang), ct);
            return $"Blocked app {norm} for {user.Username}";
        }
        else
        {
            BlockedAppRepository.Remove(user.Id, norm);
            var msg = LocalizationManager.T("cmd_app_unblocked", lang, norm, user.Username);
            await _client.SendMessageAsync(token, chatId, $"✅ {msg}", BuildMainKeyboard(lang), ct);
            return $"Unblocked app {norm} for {user.Username}";
        }
    }

    private async Task<string?> RemoteLockScreenAsync(string token, string chatId, int sessionId, string lang, CancellationToken ct)
    {
        if (!_sessionTracker.ActiveSessions.TryGetValue(sessionId, out var session))
        {
            await _client.SendMessageAsync(token, chatId, "⚠️ Session no longer active.", BuildMainKeyboard(lang), ct);
            return null;
        }

        var ok = SessionManager.DisconnectSession(sessionId);
        var msg = ok ? LocalizationManager.T("cmd_locked", lang, Escape(session.Username)) : "⚠️ Failed to lock screen.";
        await _client.SendMessageAsync(token, chatId, msg, BuildMainKeyboard(lang), ct);
        return ok ? $"Locked {session.Username}" : null;
    }

    private async Task<string?> RemoteLogoffAsync(string token, string chatId, int sessionId, string lang, CancellationToken ct)
    {
        if (!_sessionTracker.ActiveSessions.TryGetValue(sessionId, out var session))
        {
            await _client.SendMessageAsync(token, chatId, "⚠️ Session no longer active.", BuildMainKeyboard(lang), ct);
            return null;
        }

        var ok = SessionManager.ForceLogoff(sessionId);
        if (ok)
        {
            EventRepository.LogEvent(session.UserSid, EventType.FORCED_LOGOUT, "Remote logoff via Telegram");
            _sessionTracker.RemoveSession(sessionId);
            var msg = LocalizationManager.T("cmd_logged_out", lang, Escape(session.Username));
            await _client.SendMessageAsync(token, chatId, msg, BuildMainKeyboard(lang), ct);
            return $"Logged off {session.Username}";
        }
        else
        {
            await _client.SendMessageAsync(token, chatId, "⚠️ Failed to log off user.", BuildMainKeyboard(lang), ct);
            return null;
        }
    }

    private static LimitConfig EnsureOrGetLimit(int userId)
    {
        return LimitRepository.GetByUserId(userId) ?? new LimitConfig
        {
            UserId = userId,
            DailyMinutes = 120,
            ScheduleStart = new TimeOnly(8, 0),
            ScheduleEnd = new TimeOnly(22, 0)
        };
    }

    private static bool TryParseTwoPartInt(string command, string prefix, out int value)
    {
        value = 0;
        return command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(command.AsSpan(prefix.Length), out value);
    }

    private static bool TryParseThreePartInts(string command, string prefix, out int first, out int second)
    {
        first = 0;
        second = 0;
        if (!command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var rest = command[prefix.Length..];
        var parts = rest.Split(':', 2);
        return parts.Length == 2 && int.TryParse(parts[0], out first) && int.TryParse(parts[1], out second);
    }

    private static bool TryParseScheduleRange(string input, out TimeOnly start, out TimeOnly end)
    {
        start = default;
        end = default;
        var parts = input.Split('-', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 &&
               TimeOnly.TryParse(parts[0], out start) &&
               TimeOnly.TryParse(parts[1], out end) &&
               start < end;
    }

    private static object BuildMainKeyboard(string lang) => new
    {
        inline_keyboard = new object[][]
        {
            [
                new { text = LocalizationManager.T("btn_status", lang), callback_data = "cmd:status" },
                new { text = LocalizationManager.T("btn_screenshot", lang), callback_data = "cmd:screenshot" }
            ],
            [
                new { text = LocalizationManager.T("btn_top_activity", lang), callback_data = "cmd:top" },
                new { text = LocalizationManager.T("btn_dinner_pause", lang), callback_data = "cmd:pause" }
            ],
            [
                new { text = LocalizationManager.T("btn_time_limits", lang), callback_data = "cmd:limits" },
                new { text = LocalizationManager.T("btn_win_restrictions", lang), callback_data = "cmd:policies" }
            ],
            [
                new { text = LocalizationManager.T("btn_family_dns", lang), callback_data = "cmd:dns" },
                new { text = LocalizationManager.T("btn_events", lang), callback_data = "cmd:events" }
            ],
            [
                new { text = LocalizationManager.T("btn_language", lang), callback_data = "cmd:language" },
                new { text = LocalizationManager.T("btn_check_update", lang), callback_data = "cmd:update" }
            ]
        }
    };

    private async Task SendTopActivityAsync(string token, string chatId, string lang, CancellationToken ct)
    {
        var users = UserRepository.GetAll().Where(u => u.IsRestricted).ToList();
        if (users.Count == 0)
        {
            users = UserRepository.GetAll().ToList();
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var sb = new StringBuilder();

        sb.AppendLine(lang == "en" ? $"📊 <b>Today's Activity & Screen Time ({today:yyyy-MM-dd})</b>" :
                      lang == "fr" ? $"📊 <b>Activité et temps d'écran aujourd'hui ({today:yyyy-MM-dd})</b>" :
                      $"📊 <b>نشاط واستخدام التطبيقات والمواقع اليوم ({today:yyyy-MM-dd})</b>");
        sb.AppendLine();

        foreach (var user in users)
        {
            var topApps = ActivityRepository.GetTopApps(user.Id, today, 5);
            var topSites = ActivityRepository.GetTopSites(user.Id, today, 5);

            sb.AppendLine($"👤 <b>{Escape(user.Username)}:</b>");

            sb.AppendLine(lang == "en" ? "  🎮 <b>Most Used Apps:</b>" :
                          lang == "fr" ? "  🎮 <b>Applications les plus utilisées:</b>" :
                          "  🎮 <b>أكثر البرامج استخداماً:</b>");

            if (topApps.Count == 0)
            {
                sb.AppendLine(lang == "en" ? "    <i>No application activity recorded yet.</i>" :
                              lang == "fr" ? "    <i>Aucune activité d'application enregistrée.</i>" :
                              "    <i>لم يُسجل أي نشاط للتطبيقات بعد.</i>");
            }
            else
            {
                for (var i = 0; i < topApps.Count; i++)
                {
                    var a = topApps[i];
                    sb.AppendLine($"    {i + 1}. <code>{Escape(a.Name)}</code> — {FormatDuration(a.DurationSeconds)}");
                }
            }

            sb.AppendLine();
            sb.AppendLine(lang == "en" ? "  🌐 <b>Most Visited Sites & Topics:</b>" :
                          lang == "fr" ? "  🌐 <b>Sites & pages les plus consultés:</b>" :
                          "  🌐 <b>أكثر المواقع والأنشطة تصفحاً:</b>");

            if (topSites.Count == 0)
            {
                sb.AppendLine(lang == "en" ? "    <i>No web browsing activity recorded yet.</i>" :
                              lang == "fr" ? "    <i>Aucun site web enregistré.</i>" :
                              "    <i>لم يُسجل أي تصفح للمواقع بعد.</i>");
            }
            else
            {
                for (var i = 0; i < topSites.Count; i++)
                {
                    var s = topSites[i];
                    sb.AppendLine($"    {i + 1}. <b>{Escape(s.Name)}</b> — {FormatDuration(s.DurationSeconds)}");
                }
            }

            sb.AppendLine();
        }

        await _client.SendMessageAsync(token, chatId, sb.ToString(), BuildMainKeyboard(lang), ct);
    }

    private async Task<string?> HandleMsgCommandAsync(string token, string chatId, string rawText, string lang, CancellationToken ct)
    {
        var parts = rawText.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            var hint = lang == "en" ? "ℹ️ Syntax: <code>/msg <user> <message></code>\nExample: <code>/msg PC Time for dinner!</code>" :
                       lang == "fr" ? "ℹ️ Syntaxe: <code>/msg <utilisateur> <message></code>\nExemple: <code>/msg PC L'heure du dîner !</code>" :
                       "ℹ️ الصيغة: <code>/msg <المستخدم> <نص الرسالة></code>\nمثال: <code>/msg PC حان وقت الصلاة يا بطل</code>";
            await _client.SendMessageAsync(token, chatId, hint, BuildMainKeyboard(lang), ct);
            return null;
        }

        var username = parts[1];
        var text = parts[2];

        var ok = _overlayHost.SendMessageToUser(username, text);
        if (ok)
        {
            var reply = lang == "en" ? $"✅ Message sent to <b>{Escape(username)}</b>'s screen!" :
                        lang == "fr" ? $"✅ Message envoyé sur l'écran de <b>{Escape(username)}</b> !" :
                        $"✅ تم إرسال الرسالة إلى شاشة <b>{Escape(username)}</b> بنجاح!";
            await _client.SendMessageAsync(token, chatId, reply, BuildMainKeyboard(lang), ct);
            return $"Sent message to {username}";
        }
        else
        {
            var reply = lang == "en" ? $"⚠️ User <b>{Escape(username)}</b> is not currently logged in or active." :
                        lang == "fr" ? $"⚠️ L'utilisateur <b>{Escape(username)}</b> n'est pas connecté actuellement." :
                        $"⚠️ المستخدم <b>{Escape(username)}</b> غير متصل حالياً.";
            await _client.SendMessageAsync(token, chatId, reply, BuildMainKeyboard(lang), ct);
            return null;
        }
    }

    private async Task<string?> HandlePauseResumeCommandAsync(string token, string chatId, string cmd, string[] parts, string lang, CancellationToken ct)
    {
        if (parts.Length < 2)
        {
            var hint = lang == "en" ? "ℹ️ Syntax: <code>/pause <user> [minutes]</code> or <code>/resume <user></code>\nExample: <code>/pause PC 30</code>" :
                       lang == "fr" ? "ℹ️ Syntaxe: <code>/pause <utilisateur> [minutes]</code> ou <code>/resume <utilisateur></code>" :
                       "ℹ️ الصيغة: <code>/pause <المستخدم> [دقائق]</code> أو <code>/resume <المستخدم></code>\nمثال: <code>/pause PC 30</code>";
            await _client.SendMessageAsync(token, chatId, hint, BuildMainKeyboard(lang), ct);
            return null;
        }

        var username = parts[1];
        if (cmd == "/pause")
        {
            var minutes = 30;
            if (parts.Length >= 3 && int.TryParse(parts[2], out var m) && m > 0)
            {
                minutes = m;
            }

            var ok = _overlayHost.PauseUser(username, minutes);
            if (ok)
            {
                var reply = lang == "en" ? $"⏸️ Workstation paused for <b>{Escape(username)}</b> for {minutes} min (Dinner / Break Mode)." :
                            lang == "fr" ? $"⏸️ Session mise en pause pour <b>{Escape(username)}</b> pendant {minutes} min (Mode dîner / pause)." :
                            $"⏸️ تم تفعيل وضع الاستراحة / العشاء لـ <b>{Escape(username)}</b> لمدة {minutes} دقيقة وتم قفل الشاشة.";
                await _client.SendMessageAsync(token, chatId, reply, BuildMainKeyboard(lang), ct);
                return $"Paused {username} for {minutes}m";
            }
            else
            {
                var reply = lang == "en" ? $"⚠️ User <b>{Escape(username)}</b> is not currently active." :
                            lang == "fr" ? $"⚠️ L'utilisateur <b>{Escape(username)}</b> n'est pas actif." :
                            $"⚠️ المستخدم <b>{Escape(username)}</b> غير نشط حالياً.";
                await _client.SendMessageAsync(token, chatId, reply, BuildMainKeyboard(lang), ct);
                return null;
            }
        }
        else
        {
            var ok = _overlayHost.ResumeUser(username);
            var reply = lang == "en" ? $"▶️ Pause mode lifted for <b>{Escape(username)}</b>. Screen unlocked." :
                        lang == "fr" ? $"▶️ Pause levée pour <b>{Escape(username)}</b>. Écran déverrouillé." :
                        $"▶️ تم إلغاء وضع الاستراحة للمستخدم <b>{Escape(username)}</b> وإعادة فتح الجهاز.";
            await _client.SendMessageAsync(token, chatId, reply, BuildMainKeyboard(lang), ct);
            return $"Resumed {username}";
        }
    }

    private async Task HandleQuickDinnerPauseAsync(string token, string chatId, string lang, CancellationToken ct)
    {
        var targetSession = _sessionTracker.ActiveSessions.Values.FirstOrDefault();
        if (targetSession is null)
        {
            var reply = lang == "en" ? "ℹ️ No active user sessions to pause." :
                        lang == "fr" ? "ℹ️ Aucune session active à mettre en pause." :
                        "ℹ️ لا توجد جلسات مستخدمين نشطة حالياً لتفعيل الاستراحة.";
            await _client.SendMessageAsync(token, chatId, reply, BuildMainKeyboard(lang), ct);
            return;
        }

        _overlayHost.PauseUser(targetSession.Username, 30);
        var msg = lang == "en" ? $"⏸️ Workstation paused for <b>{Escape(targetSession.Username)}</b> for 30 minutes (Dinner / Break Mode)." :
                  lang == "fr" ? $"⏸️ Session mise en pause pour <b>{Escape(targetSession.Username)}</b> pendant 30 min (Mode dîner / pause)." :
                  $"⏸️ تم تفعيل وضع الاستراحة / العشاء لـ <b>{Escape(targetSession.Username)}</b> لمدة 30 دقيقة وتم قفل الشاشة.";
        await _client.SendMessageAsync(token, chatId, msg, BuildMainKeyboard(lang), ct);
    }

    private async Task<string?> HandleDnsCommandAsync(string token, string chatId, string[] parts, string lang, CancellationToken ct)
    {
        var enable = true;
        if (parts.Length >= 2)
        {
            enable = parts[1].Equals("on", StringComparison.OrdinalIgnoreCase) ||
                     parts[1].Equals("enable", StringComparison.OrdinalIgnoreCase) ||
                     parts[1].Equals("1", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            enable = !DnsProtectionManager.IsEnabled();
        }

        DnsProtectionManager.SetFamilyDns(enable);
        var stateStr = enable ? "ON ✅" : "OFF ⚪";
        var reply = lang == "en" ? $"🛡️ Family Safe DNS (Adult & Malware Blocker): <b>{stateStr}</b>" :
                    lang == "fr" ? $"🛡️ Filtrage DNS Familial (Sites adultes & malware) : <b>{stateStr}</b>" :
                    $"🛡️ حجب المواقع الضارة والإباحية عبر DNS العائلي: <b>{stateStr}</b>";
        await _client.SendMessageAsync(token, chatId, reply, BuildMainKeyboard(lang), ct);
        return $"Toggled DNS to {enable}";
    }

    private static string FormatDuration(int seconds)
    {
        var mins = seconds / 60;
        if (mins < 1) return "< 1m";
        var h = mins / 60;
        var m = mins % 60;
        return h > 0 ? $"{h}h {m}m" : $"{m}m";
    }

    private static string FormatMinutes(int totalMinutes) =>
        totalMinutes >= 60 ? $"{totalMinutes / 60}h {totalMinutes % 60}m" : $"{totalMinutes}m";

    private static string FormatSpan(TimeSpan ts) =>
        ts.TotalDays >= 1
            ? $"{(int)ts.TotalDays}d {ts.Hours:D2}h {ts.Minutes:D2}m"
            : $"{ts.Hours:D2}h {ts.Minutes:D2}m {ts.Seconds:D2}s";

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
