using System.Diagnostics;
using System.Text;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;
using ParentalControl.Core.Platform;
using ParentalControl.Core.Telegram;

namespace ParentalControl.Service;

/// <summary>
/// Long-polling Telegram bot worker providing device status, live screenshots,
/// remaining time &amp; policy inspection, and remote session control.
/// </summary>
public sealed class TelegramBotWorker : BackgroundService
{
    private readonly TelegramApiClient _client;
    private readonly SessionTracker _sessionTracker;
    private readonly OverlayHostWorker _overlayHost;
    private readonly Serilog.ILogger _logger;
    private readonly DateTime _serviceStartedAt = DateTime.Now;
    private readonly Dictionary<string, (string Mode, int UserId)> _pendingInputs = new();
    private long _updateOffset;

    public TelegramBotWorker(
        TelegramApiClient client,
        SessionTracker sessionTracker,
        OverlayHostWorker overlayHost,
        Serilog.ILogger logger)
    {
        _client = client;
        _sessionTracker = sessionTracker;
        _overlayHost = overlayHost;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Telegram bot worker started");

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

                foreach (var update in updates)
                {
                    _updateOffset = Math.Max(_updateOffset, update.UpdateId + 1);
                    await HandleUpdateAsync(settings, update, stoppingToken);
                }
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

    private async Task HandleUpdateAsync(AppSettings settings, TelegramUpdate update, CancellationToken ct)
    {
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
                    $"✅ <b>تم ربط البوت بنجاح</b>\n🖥 الجهاز: <b>{Escape(Environment.MachineName)}</b>\n💬 Chat ID: <code>{Escape(update.ChatId)}</code>",
                    ct);
            }
            else
            {
                await _client.SendMessageAsync(
                    settings.TelegramBotToken,
                    update.ChatId,
                    $"ℹ️ أرسل <code>/start</code> لربط هذا الحساب بالجهاز، أو ضع Chat ID التالي في البرنامج:\n<code>{Escape(update.ChatId)}</code>",
                    ct: ct);
            }
            return;
        }

        if (!string.Equals(update.ChatId, settings.TelegramChatId, StringComparison.Ordinal))
        {
            _logger.Warning("Ignored unauthorized Telegram message from chat {ChatId}", update.ChatId);
            return;
        }

        if (!string.IsNullOrEmpty(update.CallbackQueryId))
        {
            _pendingInputs.Remove(update.ChatId);
            await _client.AnswerCallbackQueryAsync(settings.TelegramBotToken, update.CallbackQueryId, ct: ct);
            await DispatchCommandAsync(settings.TelegramBotToken, update.ChatId, update.CallbackData ?? string.Empty, ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(update.MessageText))
        {
            var rawText = update.MessageText.Trim();
            if (rawText.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                _pendingInputs.Remove(update.ChatId);
                await SendMainMenuAsync(settings.TelegramBotToken, update.ChatId, "تم إلغاء العملية.", ct);
                return;
            }

            if (!rawText.StartsWith('/') && _pendingInputs.TryGetValue(update.ChatId, out var pending))
            {
                await HandlePendingInputAsync(settings.TelegramBotToken, update.ChatId, pending.Mode, pending.UserId, rawText, ct);
                return;
            }

            _pendingInputs.Remove(update.ChatId);
            await DispatchTextCommandAsync(settings.TelegramBotToken, update.ChatId, rawText, ct);
        }
    }

    private async Task DispatchTextCommandAsync(string token, string chatId, string rawText, CancellationToken ct)
    {
        var parts = rawText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();

        if (cmd is "/addtime" or "/settime" or "/setschedule")
        {
            await HandleTextTimeCommandAsync(token, chatId, cmd, parts, ct);
            return;
        }

        if (cmd is "/block" or "/unblock")
        {
            await HandleTextBlockAppCommandAsync(token, chatId, cmd, parts, ct);
            return;
        }

        await DispatchCommandAsync(token, chatId, cmd, ct);
    }

    private async Task DispatchCommandAsync(string token, string chatId, string command, CancellationToken ct)
    {
        switch (command)
        {
            case "/start":
            case "/menu":
            case "/help":
            case "cmd:menu":
                await SendMainMenuAsync(token, chatId,
                    $"🛡 <b>لوحة تحكم الرقابة الأبوية</b>\n🖥 الجهاز: <b>{Escape(Environment.MachineName)}</b>", ct);
                break;

            case "/status":
            case "cmd:status":
                await SendStatusAsync(token, chatId, ct);
                break;

            case "/screenshot":
            case "cmd:screenshot":
                await SendScreenshotAsync(token, chatId, ct);
                break;

            case "/limits":
            case "cmd:limits":
                await SendLimitsAsync(token, chatId, ct);
                break;

            case "/policies":
            case "cmd:policies":
                await SendPoliciesAsync(token, chatId, ct);
                break;

            case "/events":
            case "cmd:events":
                await SendEventsAsync(token, chatId, ct);
                break;

            default:
                if (TryParseTwoPartInt(command, "time_menu:", out var menuUserId))
                {
                    await SendUserTimeMenuAsync(token, chatId, menuUserId, ct);
                }
                else if (TryParseThreePartInts(command, "adj_limit:", out var adjUserId, out var deltaMinutes))
                {
                    await AdjustDailyLimitAsync(token, chatId, adjUserId, deltaMinutes, ct);
                }
                else if (TryParseTwoPartInt(command, "reset_usage:", out var resetUserId))
                {
                    await ResetTodayUsageAsync(token, chatId, resetUserId, ct);
                }
                else if (TryParseTwoPartInt(command, "remove_limit:", out var removeUserId))
                {
                    await RemoveUserLimitAsync(token, chatId, removeUserId, ct);
                }
                else if (TryParseTwoPartInt(command, "prompt_limit:", out var promptLimitUserId))
                {
                    _pendingInputs[chatId] = ("limit", promptLimitUserId);
                    await _client.SendMessageAsync(token, chatId,
                        "✏️ أرسل الحد اليومي الجديد بالدقائق (مثال: <code>90</code> أو <code>120</code>)، أو أرسل /cancel للإلغاء.", ct: ct);
                }
                else if (TryParseTwoPartInt(command, "prompt_usage:", out var promptUsageUserId))
                {
                    _pendingInputs[chatId] = ("usage", promptUsageUserId);
                    await _client.SendMessageAsync(token, chatId,
                        "📝 أرسل عدد الدقائق المستخدمة اليوم (مثال: <code>0</code> أو <code>30</code>)، أو أرسل /cancel للإلغاء.", ct: ct);
                }
                else if (TryParseTwoPartInt(command, "prompt_schedule:", out var promptSchedUserId))
                {
                    _pendingInputs[chatId] = ("schedule", promptSchedUserId);
                    await _client.SendMessageAsync(token, chatId,
                        "🕒 أرسل الجدول الزمني المسموح بصيغة <code>HH:mm-HH:mm</code> (مثال: <code>08:00-21:30</code>)، أو أرسل /cancel للإلغاء.", ct: ct);
                }
                else if (TryParseThreePartInts(command, "approve_time:", out var approveUserId, out var approveMin))
                {
                    await AdjustDailyLimitAsync(token, chatId, approveUserId, approveMin, ct);
                }
                else if (TryParseTwoPartInt(command, "deny_time:", out var denyUserId))
                {
                    var deniedUser = UserRepository.GetAll().FirstOrDefault(u => u.Id == denyUserId);
                    await _client.SendMessageAsync(token, chatId,
                        $"❌ تم رفض طلب الوقت الإضافي للمستخدم <b>{Escape(deniedUser?.Username ?? denyUserId.ToString())}</b>.",
                        BuildMainKeyboard(), ct);
                }
                else if (TryParseTwoPartInt(command, "lock_screen:", out var lockSessionId))
                {
                    await RemoteLockScreenAsync(token, chatId, lockSessionId, ct);
                }
                else if (TryParseTwoPartInt(command, "logoff:", out var logoffSessionId))
                {
                    await RemoteLogoffAsync(token, chatId, logoffSessionId, ct);
                }
                else
                {
                    await SendMainMenuAsync(token, chatId, "اختر أمراً من القائمة:", ct);
                }
                break;
        }
    }

    private async Task SendMainMenuAsync(string token, string chatId, string headerText, CancellationToken ct)
    {
        await _client.SendMessageAsync(token, chatId, headerText, BuildMainKeyboard(), ct);
    }

    private async Task SendStatusAsync(string token, string chatId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var serviceUptime = DateTime.Now - _serviceStartedAt;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var now = DateTime.Now;

        sb.AppendLine($"🖥 <b>حالة الجهاز: {Escape(Environment.MachineName)}</b>");
        sb.AppendLine($"🟢 الاتصال: <b>شغّال ومتصل</b>");
        sb.AppendLine($"⏱ مدة تشغيل الويندوز: <code>{FormatSpan(uptime)}</code>");
        sb.AppendLine($"🛡 مدة عمل الخدمة: <code>{FormatSpan(serviceUptime)}</code>");
        sb.AppendLine();

        var actionRows = new List<object[]>();

        if (_sessionTracker.ActiveSessions.Count == 0)
        {
            sb.AppendLine("👤 <b>الجلسات النشطة:</b> لا يوجد مستخدم مسجّل دخول حالياً.");
        }
        else
        {
            sb.AppendLine("👤 <b>الجلسات النشطة الآن:</b>");
            foreach (var (sessionId, session) in _sessionTracker.ActiveSessions)
            {
                var user = UserRepository.GetBySid(session.UserSid);
                var limit = user is not null ? LimitRepository.GetByUserId(user.Id) : null;
                var usage = user is not null ? UsageRepository.GetUsage(user.Id, today) : null;
                var used = usage?.MinutesUsed ?? 0;

                sb.AppendLine($"• <b>{Escape(session.Username)}</b> (Session #{sessionId})");
                if (user is not null && user.IsRestricted && limit is not null)
                {
                    var dailyLeftMin = Math.Max(0, limit.DailyMinutes - used);
                    var schedEndDt = now.Date.Add(limit.ScheduleEnd.ToTimeSpan());
                    var schedLeftMin = Math.Max(0, (int)(schedEndDt - now).TotalMinutes);
                    var effectiveLeft = Math.Min(dailyLeftMin, schedLeftMin);

                    sb.AppendLine($"  🔒 مقيّد | المستخدم اليوم: <code>{used}/{limit.DailyMinutes}</code> د");
                    sb.AppendLine($"  ⏳ المتبقي للجلسة: <code>{FormatMinutes(effectiveLeft)}</code> (حتى {limit.ScheduleEnd:HH:mm})");

                    actionRows.Add(
                    [
                        new { text = $"⏱ تعديل الوقت ({session.Username})", callback_data = $"time_menu:{user.Id}" },
                        new { text = $"🖥 قفل ({session.Username})", callback_data = $"lock_screen:{sessionId}" },
                        new { text = $"🔒 خروج ({session.Username})", callback_data = $"logoff:{sessionId}" }
                    ]);
                }
                else
                {
                    sb.AppendLine($"  🔓 غير مقيّد | الاستخدام اليوم: <code>{used}</code> د");
                    var rowButtons = new List<object>();
                    if (user is not null)
                    {
                        rowButtons.Add(new { text = $"⏱ إدارة الوقت ({session.Username})", callback_data = $"time_menu:{user.Id}" });
                    }
                    rowButtons.Add(new { text = $"🖥 قفل ({session.Username})", callback_data = $"lock_screen:{sessionId}" });
                    rowButtons.Add(new { text = $"🔒 خروج ({session.Username})", callback_data = $"logoff:{sessionId}" });
                    actionRows.Add(rowButtons.ToArray());
                }
            }
        }

        actionRows.Add(
        [
            new { text = "📸 لقطة شاشة", callback_data = "cmd:screenshot" },
            new { text = "🔄 تحديث", callback_data = "cmd:status" }
        ]);
        actionRows.Add([new { text = "🏠 القائمة الرئيسية", callback_data = "cmd:menu" }]);

        await _client.SendMessageAsync(token, chatId, sb.ToString(), new { inline_keyboard = actionRows }, ct);
    }

    private async Task SendScreenshotAsync(string token, string chatId, CancellationToken ct)
    {
        await _client.SendMessageAsync(token, chatId, "📸 جاري التقاط لقطة شاشة...", ct: ct);

        var shot = await _overlayHost.CaptureScreenshotAsync(ct);
        if (shot is null)
        {
            await _client.SendMessageAsync(
                token,
                chatId,
                "⚠️ تعذّر التقاط الشاشة. قد لا تكون هناك جلسة سطح مكتب نشطة حالياً (الشاشة مقفلة أو لا يوجد مستخدم).",
                BuildMainKeyboard(),
                ct);
            return;
        }

        var (bytes, username, _) = shot.Value;
        var caption = $"📸 {Environment.MachineName} | 👤 {username} | 🕒 {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        await _client.SendPhotoAsync(token, chatId, bytes, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png", caption, ct);
    }

    private async Task SendLimitsAsync(string token, string chatId, CancellationToken ct)
    {
        var users = UserRepository.GetAll();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var sb = new StringBuilder();
        sb.AppendLine($"⏱ <b>حدود الاستخدام اليومي — {Escape(Environment.MachineName)}</b>\n");

        if (users.Count == 0)
        {
            sb.AppendLine("لا توجد حسابات قياسية مسجلة في قاعدة البيانات.");
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
                    sb.AppendLine($"  • المستخدم اليوم: <code>{used}/{limit.DailyMinutes}</code> د");
                    sb.AppendLine($"  • المتبقي اليوم: <code>{FormatMinutes(remaining)}</code>");
                    sb.AppendLine($"  • الأوقات المسموحة: <code>{limit.ScheduleStart:HH:mm} - {limit.ScheduleEnd:HH:mm}</code>");
                }
                else
                {
                    sb.AppendLine($"  • بلا حدود زمنية (الاستخدام اليوم: <code>{used}</code> د)");
                }
                sb.AppendLine();
            }
        }

        var keyboardRows = new List<object[]>();
        foreach (var u in users)
        {
            keyboardRows.Add([new { text = $"⚙️ زيادة / إنقاص / تعديل وقت: {u.Username}", callback_data = $"time_menu:{u.Id}" }]);
        }
        keyboardRows.Add(
        [
            new { text = "🔄 تحديث", callback_data = "cmd:limits" },
            new { text = "🏠 القائمة الرئيسية", callback_data = "cmd:menu" }
        ]);

        await _client.SendMessageAsync(token, chatId, sb.ToString(), new { inline_keyboard = keyboardRows }, ct);
    }

    private async Task SendPoliciesAsync(string token, string chatId, CancellationToken ct)
    {
        var users = UserRepository.GetAll();
        var sb = new StringBuilder();
        sb.AppendLine($"⚙️ <b>قيود ويندوز المفعّلة — {Escape(Environment.MachineName)}</b>\n");

        if (users.Count == 0)
        {
            sb.AppendLine("لا توجد حسابات قياسية مسجلة.");
        }
        else
        {
            foreach (var u in users)
            {
                var p = UserPolicyRepository.GetByUserId(u.Id);
                var apps = BlockedAppRepository.GetByUserId(u.Id);
                sb.AppendLine($"👤 <b>{Escape(u.Username)}</b>");
                sb.AppendLine($"  {(p?.LockTaskbar == true ? "✅" : "⬜")} قفل شريط المهام");
                sb.AppendLine($"  {(p?.BlockSettings == true ? "✅" : "⬜")} منع الإعدادات ولوحة التحكم");
                sb.AppendLine($"  {(p?.BlockTaskManager == true ? "✅" : "⬜")} منع مدير المهام (Task Manager)");
                sb.AppendLine($"  {(p?.BlockCommandPrompt == true ? "✅" : "⬜")} منع موجه الأوامر (CMD)");
                sb.AppendLine(apps.Count > 0
                    ? $"  🚫 البرامج المحظورة: <code>{Escape(string.Join(", ", apps))}</code>"
                    : "  🚫 البرامج المحظورة: لا يوجد");
                sb.AppendLine();
            }
        }

        await _client.SendMessageAsync(token, chatId, sb.ToString(), BuildMainKeyboard(), ct);
    }

    private async Task SendEventsAsync(string token, string chatId, CancellationToken ct)
    {
        var events = EventRepository.GetEvents().Take(10).ToList();
        var usersBySid = UserRepository.GetAll().ToDictionary(u => u.Sid, u => u.Username);

        var sb = new StringBuilder();
        sb.AppendLine($"📋 <b>آخر 10 أحداث — {Escape(Environment.MachineName)}</b>\n");

        if (events.Count == 0)
        {
            sb.AppendLine("لا توجد أحداث مسجلة بعد.");
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

        await _client.SendMessageAsync(token, chatId, sb.ToString(), BuildMainKeyboard(), ct);
    }

    private async Task SendUserTimeMenuAsync(string token, string chatId, int userId, CancellationToken ct, string? notice = null)
    {
        var user = UserRepository.GetAll().FirstOrDefault(u => u.Id == userId);
        if (user is null)
        {
            await _client.SendMessageAsync(token, chatId, "⚠️ المستخدم غير موجود.", BuildMainKeyboard(), ct);
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

        sb.AppendLine($"⏱ <b>إدارة وقت المستخدم: {Escape(user.Username)}</b>");
        sb.AppendLine($"• الحالة: {(user.IsRestricted ? "🔒 مقيّد" : "🔓 غير مقيّد")}");
        if (limit is not null)
        {
            var remaining = Math.Max(0, limit.DailyMinutes - used);
            sb.AppendLine($"• الحد اليومي: <code>{limit.DailyMinutes}</code> دقيقة ({FormatMinutes(limit.DailyMinutes)})");
            sb.AppendLine($"• المستخدم اليوم: <code>{used}</code> دقيقة");
            sb.AppendLine($"• المتبقي اليوم: <code>{FormatMinutes(remaining)}</code>");
            sb.AppendLine($"• الجدول المسموح: <code>{limit.ScheduleStart:HH:mm} - {limit.ScheduleEnd:HH:mm}</code>");
        }
        else
        {
            sb.AppendLine($"• لا يوجد حد يومي مضبوط (المستخدم اليوم: <code>{used}</code> د)");
        }

        var keyboard = new
        {
            inline_keyboard = new object[][]
            {
                [
                    new { text = "➕ +15 د", callback_data = $"adj_limit:{userId}:15" },
                    new { text = "➕ +30 د", callback_data = $"adj_limit:{userId}:30" },
                    new { text = "➕ +60 د", callback_data = $"adj_limit:{userId}:60" }
                ],
                [
                    new { text = "➖ -15 د", callback_data = $"adj_limit:{userId}:-15" },
                    new { text = "➖ -30 د", callback_data = $"adj_limit:{userId}:-30" },
                    new { text = "➖ -60 د", callback_data = $"adj_limit:{userId}:-60" }
                ],
                [
                    new { text = "✏️ تعيين الحد (دقائق)", callback_data = $"prompt_limit:{userId}" },
                    new { text = "🕒 تعديل الجدول", callback_data = $"prompt_schedule:{userId}" }
                ],
                [
                    new { text = "📝 تعديل الاستخدام اليوم", callback_data = $"prompt_usage:{userId}" },
                    new { text = "🔄 تصفير اليوم", callback_data = $"reset_usage:{userId}" }
                ],
                [
                    new { text = "🔓 إزالة الحد الزمني", callback_data = $"remove_limit:{userId}" },
                    new { text = "⬅️ رجوع", callback_data = "cmd:limits" }
                ]
            }
        };

        await _client.SendMessageAsync(token, chatId, sb.ToString(), keyboard, ct);
    }

    private async Task AdjustDailyLimitAsync(string token, string chatId, int userId, int deltaMinutes, CancellationToken ct)
    {
        var limit = EnsureOrGetLimit(userId);
        limit.DailyMinutes = Math.Clamp(limit.DailyMinutes + deltaMinutes, 1, 1440);
        LimitRepository.Upsert(limit);
        UserRepository.SetRestricted(userId, true);

        var sign = deltaMinutes >= 0 ? $"+{deltaMinutes}" : deltaMinutes.ToString();
        await SendUserTimeMenuAsync(token, chatId, userId, ct,
            $"✅ تم تعديل الحد اليومي بمقدار <b>{sign} دقيقة</b> (الحد الجديد: <code>{limit.DailyMinutes}</code> د).");
    }

    private async Task ResetTodayUsageAsync(string token, string chatId, int userId, CancellationToken ct)
    {
        UsageRepository.SetUsage(userId, DateOnly.FromDateTime(DateTime.Now), 0);
        await SendUserTimeMenuAsync(token, chatId, userId, ct, "✅ تم تصفير دقائق الاستخدام لليوم إلى <code>0</code>.");
    }

    private async Task RemoveUserLimitAsync(string token, string chatId, int userId, CancellationToken ct)
    {
        LimitRepository.Delete(userId);
        UserRepository.SetRestricted(userId, false);
        await SendUserTimeMenuAsync(token, chatId, userId, ct, "🔓 تم إزالة الحد الزمني وأصبح المستخدم غير مقيّد.");
    }

    private async Task HandlePendingInputAsync(
        string token,
        string chatId,
        string mode,
        int userId,
        string rawText,
        CancellationToken ct)
    {
        if (mode == "limit")
        {
            if (!int.TryParse(rawText, out var minutes) || minutes < 1 || minutes > 1440)
            {
                await _client.SendMessageAsync(token, chatId,
                    "⚠️ أدخل رقماً صحيحاً بين <code>1</code> و<code>1440</code> دقيقة (أو /cancel).", ct: ct);
                return;
            }

            _pendingInputs.Remove(chatId);
            var limit = EnsureOrGetLimit(userId);
            limit.DailyMinutes = minutes;
            LimitRepository.Upsert(limit);
            UserRepository.SetRestricted(userId, true);
            await SendUserTimeMenuAsync(token, chatId, userId, ct,
                $"✅ تم تعيين الحد اليومي إلى <b>{minutes} دقيقة</b>.");
            return;
        }

        if (mode == "usage")
        {
            if (!int.TryParse(rawText, out var usedMinutes) || usedMinutes < 0 || usedMinutes > 1440)
            {
                await _client.SendMessageAsync(token, chatId,
                    "⚠️ أدخل رقماً صحيحاً بين <code>0</code> و<code>1440</code> دقيقة (أو /cancel).", ct: ct);
                return;
            }

            _pendingInputs.Remove(chatId);
            UsageRepository.SetUsage(userId, DateOnly.FromDateTime(DateTime.Now), usedMinutes);
            await SendUserTimeMenuAsync(token, chatId, userId, ct,
                $"✅ تم تعديل الاستخدام اليوم إلى <b>{usedMinutes} دقيقة</b>.");
            return;
        }

        if (mode == "schedule")
        {
            if (!TryParseScheduleRange(rawText, out var start, out var end))
            {
                await _client.SendMessageAsync(token, chatId,
                    "⚠️ الصيغة غير صحيحة. استخدم <code>HH:mm-HH:mm</code> مثل <code>08:00-21:30</code> (البداية قبل النهاية) أو /cancel.", ct: ct);
                return;
            }

            _pendingInputs.Remove(chatId);
            var limit = EnsureOrGetLimit(userId);
            limit.ScheduleStart = start;
            limit.ScheduleEnd = end;
            LimitRepository.Upsert(limit);
            UserRepository.SetRestricted(userId, true);
            await SendUserTimeMenuAsync(token, chatId, userId, ct,
                $"✅ تم تحديث الجدول المسموح إلى <code>{start:HH:mm} - {end:HH:mm}</code>.");
        }
    }

    private async Task HandleTextTimeCommandAsync(
        string token,
        string chatId,
        string cmd,
        string[] parts,
        CancellationToken ct)
    {
        if (parts.Length < 3)
        {
            await _client.SendMessageAsync(token, chatId,
                "ℹ️ الصيغة:\n<code>/addtime PC 30</code> أو <code>/addtime PC -15</code>\n<code>/settime PC 120</code>\n<code>/setschedule PC 08:00-21:30</code>",
                BuildMainKeyboard(), ct);
            return;
        }

        var username = parts[1];
        var valueArg = parts[2];
        var user = UserRepository.GetAll()
            .FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            await _client.SendMessageAsync(token, chatId,
                $"⚠️ لم يتم العثور على المستخدم <code>{Escape(username)}</code>.", BuildMainKeyboard(), ct);
            return;
        }

        if (cmd == "/addtime" && int.TryParse(valueArg, out var delta))
        {
            await AdjustDailyLimitAsync(token, chatId, user.Id, delta, ct);
        }
        else if (cmd == "/settime" && int.TryParse(valueArg, out var minutes) && minutes is >= 1 and <= 1440)
        {
            var limit = EnsureOrGetLimit(user.Id);
            limit.DailyMinutes = minutes;
            LimitRepository.Upsert(limit);
            UserRepository.SetRestricted(user.Id, true);
            await SendUserTimeMenuAsync(token, chatId, user.Id, ct,
                $"✅ تم تعيين الحد اليومي إلى <b>{minutes} دقيقة</b>.");
        }
        else if (cmd == "/setschedule" && TryParseScheduleRange(valueArg, out var start, out var end))
        {
            var limit = EnsureOrGetLimit(user.Id);
            limit.ScheduleStart = start;
            limit.ScheduleEnd = end;
            LimitRepository.Upsert(limit);
            UserRepository.SetRestricted(user.Id, true);
            await SendUserTimeMenuAsync(token, chatId, user.Id, ct,
                $"✅ تم تحديث الجدول المسموح إلى <code>{start:HH:mm} - {end:HH:mm}</code>.");
        }
        else
        {
            await _client.SendMessageAsync(token, chatId, "⚠️ قيمة غير صالحة للأمر.", BuildMainKeyboard(), ct);
        }
    }

    private async Task HandleTextBlockAppCommandAsync(
        string token,
        string chatId,
        string cmd,
        string[] parts,
        CancellationToken ct)
    {
        if (parts.Length < 3)
        {
            await _client.SendMessageAsync(token, chatId,
                "ℹ️ الصيغة:\n<code>/block PC robloxplayerbeta</code>\n<code>/unblock PC robloxplayerbeta</code>",
                BuildMainKeyboard(), ct);
            return;
        }

        var username = parts[1];
        var appName = parts[2];
        var user = UserRepository.GetAll()
            .FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            await _client.SendMessageAsync(token, chatId,
                $"⚠️ لم يتم العثور على المستخدم <code>{Escape(username)}</code>.", BuildMainKeyboard(), ct);
            return;
        }

        var norm = BlockedAppRepository.NormalizeProcessName(appName);
        if (string.IsNullOrEmpty(norm))
        {
            await _client.SendMessageAsync(token, chatId, "⚠️ اسم التطبيق غير صالح.", BuildMainKeyboard(), ct);
            return;
        }

        if (cmd == "/block")
        {
            BlockedAppRepository.Add(user.Id, norm);
            await _client.SendMessageAsync(token, chatId,
                $"🚫 تم حظر التطبيق <code>{Escape(norm)}.exe</code> على <b>{Escape(user.Username)}</b>.",
                BuildMainKeyboard(), ct);
        }
        else
        {
            BlockedAppRepository.Remove(user.Id, norm);
            await _client.SendMessageAsync(token, chatId,
                $"✅ تم فك حظر التطبيق <code>{Escape(norm)}.exe</code> عن <b>{Escape(user.Username)}</b>.",
                BuildMainKeyboard(), ct);
        }
    }

    private async Task RemoteLockScreenAsync(string token, string chatId, int sessionId, CancellationToken ct)
    {
        if (!_sessionTracker.ActiveSessions.TryGetValue(sessionId, out var session))
        {
            await _client.SendMessageAsync(token, chatId, "⚠️ الجلسة لم تعد نشطة.", BuildMainKeyboard(), ct);
            return;
        }

        var ok = SessionManager.DisconnectSession(sessionId);
        await _client.SendMessageAsync(
            token,
            chatId,
            ok ? $"🖥 تم قفل شاشة <b>{Escape(session.Username)}</b>." : "⚠️ تعذّر قفل الشاشة.",
            BuildMainKeyboard(),
            ct);
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

    private async Task RemoteLogoffAsync(string token, string chatId, int sessionId, CancellationToken ct)
    {
        if (!_sessionTracker.ActiveSessions.TryGetValue(sessionId, out var session))
        {
            await _client.SendMessageAsync(token, chatId, "⚠️ الجلسة لم تعد نشطة.", BuildMainKeyboard(), ct);
            return;
        }

        var ok = SessionManager.ForceLogoff(sessionId);
        if (ok)
        {
            EventRepository.LogEvent(session.UserSid, EventType.FORCED_LOGOUT, "Remote logoff via Telegram");
            _sessionTracker.RemoveSession(sessionId);
            await _client.SendMessageAsync(
                token,
                chatId,
                $"🔒 تم تسجيل خروج <b>{Escape(session.Username)}</b> بنجاح.",
                BuildMainKeyboard(),
                ct);
        }
        else
        {
            await _client.SendMessageAsync(token, chatId, "⚠️ تعذّر تنفيذ تسجيل الخروج.", BuildMainKeyboard(), ct);
        }
    }

    private static object BuildMainKeyboard() => new
    {
        inline_keyboard = new object[][]
        {
            [
                new { text = "📊 حالة الجهاز", callback_data = "cmd:status" },
                new { text = "📸 لقطة شاشة", callback_data = "cmd:screenshot" }
            ],
            [
                new { text = "⏱ الوقت والحدود", callback_data = "cmd:limits" },
                new { text = "⚙️ قيود ويندوز", callback_data = "cmd:policies" }
            ],
            [
                new { text = "📋 آخر الأحداث", callback_data = "cmd:events" }
            ]
        }
    };

    private static string FormatMinutes(int totalMinutes) =>
        totalMinutes >= 60 ? $"{totalMinutes / 60}س {totalMinutes % 60}د" : $"{totalMinutes}د";

    private static string FormatSpan(TimeSpan ts) =>
        ts.TotalDays >= 1
            ? $"{(int)ts.TotalDays}d {ts.Hours:D2}h {ts.Minutes:D2}m"
            : $"{ts.Hours:D2}h {ts.Minutes:D2}m {ts.Seconds:D2}s";

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
