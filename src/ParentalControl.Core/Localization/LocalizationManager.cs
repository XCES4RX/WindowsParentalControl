namespace ParentalControl.Core.Localization;

public static class LocalizationManager
{
    public static string T(string key, string? lang = "ar", params object[] args)
    {
        var normalizedLang = (lang ?? "ar").Trim().ToLowerInvariant();
        if (normalizedLang.StartsWith("en")) normalizedLang = "en";
        else if (normalizedLang.StartsWith("fr")) normalizedLang = "fr";
        else normalizedLang = "ar";

        var dict = normalizedLang switch
        {
            "en" => English,
            "fr" => French,
            _ => Arabic
        };

        if (!dict.TryGetValue(key, out var template))
        {
            if (!Arabic.TryGetValue(key, out template))
            {
                template = key;
            }
        }

        if (args is { Length: > 0 })
        {
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        return template;
    }

    private static readonly Dictionary<string, string> Arabic = new()
    {
        // ═══ Application Shell & Headers ═══
        ["app_title"] = "نظام الرقابة الأبوية لويندوز",
        ["app_subtitle"] = "إدارة وقت الاستخدام وحماية الأطفال",
        ["btn_settings"] = "الإعدادات وتلغرام",
        ["btn_back"] = "العودة للوحة التحكم",
        ["service_active"] = "الخدمة: نشطة وشغالة",
        ["service_down"] = "الخدمة: متوقفة",
        ["telegram_active"] = "تلغرام: متصل ونشط",
        ["telegram_pairing"] = "تلغرام: بانتظار الاقتران",
        ["telegram_off"] = "تلغرام: غير مفعّل",

        // ═══ Dashboard ═══
        ["std_users"] = "حسابات المستخدمين القياسية",
        ["admin_users"] = "حسابات المسؤولين",
        ["admin_users_hint"] = "لا يمكن تقييد حسابات المسؤولين لأن لديهم صلاحيات إلغاء القيود. لإضافة قيود لأحد هذه الحسابات، قم بتغيير نوعه إلى حساب قياسي أولاً.",
        ["recent_events"] = "سجل الأحداث الأخيرة",
        ["col_timestamp"] = "الوقت والتاريخ",
        ["col_username"] = "اسم المستخدم",
        ["col_sid"] = "معرف الأمان SID",
        ["col_event"] = "نوع الحدث",
        ["col_details"] = "التفاصيل",
        ["tooltip_refresh_users"] = "تحديث قائمة المستخدمين",
        ["tooltip_refresh_events"] = "تحديث سجل الأحداث",
        ["summary_no_limit"] = "بلا حدود زمنية",
        ["summary_limit_format"] = "{0} دقيقة / يوم ({1} متبقية)",
        ["summary_no_sched"] = "بلا جدول زمني",
        ["summary_sched_format"] = "{0} إلى {1}",
        ["summary_no_restrictions"] = "بلا قيود ويندوز",
        ["summary_restrictions_count"] = "{0} قيود ويندوز مفعّلة",
        ["admin_role_text"] = "مسؤول",
        ["std_role_text"] = "مستخدم قياسي",

        // ═══ User Details ═══
        ["user_details_header"] = "تفاصيل الحساب والقيود",
        ["lbl_username"] = "اسم المستخدم",
        ["lbl_sid"] = "معرف الأمان SID",
        ["lbl_account_type"] = "نوع الحساب",
        ["lbl_restricted"] = "خاضع للقيود",
        ["lbl_today_usage"] = "استخدام اليوم",
        ["btn_set"] = "تعيين",
        ["btn_reset_zero"] = "تصفير (0)",
        ["unit_minutes"] = "دقيقة",
        ["val_yes"] = "نعم",
        ["val_no"] = "لا",

        ["sec_time_limits"] = "إدارة الوقت والجدول المسموح",
        ["chk_enable_limits"] = "تفعيل الحدود الزمنية لهذا الحساب",
        ["lbl_daily_limit"] = "الحد اليومي المسموح (بالدقائق):",
        ["lbl_sched_start"] = "بداية الوقت المسموح:",
        ["lbl_sched_end"] = "نهاية الوقت المسموح:",
        ["btn_save_time"] = "حفظ إعدادات الوقت",

        ["sec_win_restrictions"] = "قيود نظام ويندوز",
        ["chk_lock_taskbar"] = "قفل شريط المهام ومنع إعداداته",
        ["chk_block_settings"] = "منع الإعدادات ولوحة التحكم",
        ["chk_block_taskmgr"] = "منع مدير المهام (Task Manager)",
        ["chk_block_cmd"] = "منع موجه الأوامر (CMD)",
        ["btn_save_restrictions"] = "حفظ قيود ويندوز",

        ["sec_blocked_apps"] = "حظر التطبيقات والألعاب",
        ["hint_app_name"] = "اسم ملف التشغيل (مثال: robloxplayerbeta أو steam)",
        ["btn_add_app"] = "حظر التطبيق",
        ["btn_remove_app"] = "إلغاء الحظر",
        ["msg_no_blocked_apps"] = "لا توجد تطبيقات محظورة لهذا الحساب حالياً.",

        // ═══ Settings View ═══
        ["sec_language"] = "اللغة والتعريب",
        ["lbl_app_lang"] = "لغة برنامج الإدارة:",
        ["lbl_bot_lang"] = "لغة بوت تلغرام:",
        ["lang_ar_display"] = "العربية",
        ["lang_en_display"] = "English",
        ["lang_fr_display"] = "Français",
        ["btn_save_language"] = "حفظ إعدادات اللغة",

        ["sec_telegram"] = "التحكم عن بُعد عبر بوت تلغرام",
        ["chk_enable_telegram"] = "تفعيل الربط مع بوت تلغرام",
        ["hint_bot_token"] = "رمز البوت (Bot Token) أو رابط الـ API كاملاً",
        ["hint_chat_id"] = "معرف المحادثة Chat ID (اتركه فارغاً واضغط Start في البوت ثم اضغط زر الاكتشاف)",
        ["btn_detect_chat"] = "اكتشاف المعرف",
        ["chk_notify_login"] = "إرسال إشعار فوري عند تسجيل دخول المستخدم",
        ["chk_notify_logout"] = "إرسال إشعار فوري عند تسجيل خروج المستخدم",
        ["chk_notify_shutdown"] = "إرسال إشعار فوري عند إيقاف تشغيل أو إعادة تشغيل الجهاز",
        ["chk_notify_limits"] = "إرسال تنبيه عند انتهاء الوقت أو محاولات التلاعب بالساعة أو البرامج المحظورة",
        ["offline_help_text"] = "دعم الأوامر دون اتصال: يمكنك إرسال الأوامر أثناء إيقاف تشغيل الكمبيوتر، وسيتم تطبيقها تلقائياً فور تشغيل الجهاز.",
        ["btn_test_connect"] = "اختبار وربط البوت",
        ["btn_save_settings"] = "حفظ الإعدادات",

        ["sec_autoupdate"] = "التحديث التلقائي الصامت",
        ["chk_enable_autoupdate"] = "تفعيل التحديث التلقائي الصامت من خادم GitHub",
        ["autoupdate_description"] = "تتحقق الخدمة في الخلفية من الإصدارات الجديدة وتقوم بتحميلها وتثبيتها بصمت تام دون مقاطعة استخدام الكمبيوتر.",
        ["lbl_current_version"] = "الإصدار الحالي:",
        ["btn_check_updates_now"] = "فحص التحديثات الآن",

        ["sec_overlay"] = "مربع العدّ التنازلي العائم للطفل",
        ["chk_show_overlay"] = "إظهار مربع الوقت المتبقي العائم على شاشة الطفل",
        ["overlay_description"] = "يعرض بطاقة صغيرة شفافة وأنيقة في زاوية الشاشة توضح الوقت المتبقي الفعلي للجلسة مع زر لطلب وقت إضافي.",
        ["btn_save_overlay"] = "حفظ إعداد المربع العائم",

        // ═══ Bot Menus & Buttons ═══
        ["btn_status"] = "📊 حالة الجهاز",
        ["btn_screenshot"] = "📸 لقطة شاشة",
        ["btn_top_activity"] = "📊 الأكثر استخداماً",
        ["btn_dinner_pause"] = "⏸️ إيقاف عشاء (30د)",
        ["btn_send_msg"] = "💬 رسالة للشاشة",
        ["btn_family_dns"] = "🛡️ حجب المواقع (DNS)",
        ["btn_time_limits"] = "⏱ الوقت والحدود",
        ["btn_win_restrictions"] = "⚙️ قيود ويندوز",
        ["btn_events"] = "📋 آخر الأحداث",
        ["btn_language"] = "🌐 اللغة",
        ["btn_refresh"] = "🔄 تحديث",
        ["btn_main_menu"] = "🏠 القائمة الرئيسية",
        ["btn_lock"] = "🖥 قفل ({0})",
        ["btn_logout"] = "🚪 خروج ({0})",
        ["btn_adjust_time"] = "⏱ تعديل الوقت ({0})",
        ["btn_approve_15"] = "✅ موافقة (+15 د)",
        ["btn_approve_30"] = "✅ +30 د",
        ["btn_deny"] = "❌ رفض",
        ["btn_check_update"] = "🔄 فحص التحديثات",

        // ═══ Bot Main Menu & Notifications ═══
        ["menu_title"] = "🛡️ <b>لوحة تحكم الرقابة الأبوية</b>\n🖥 الجهاز: <b>{0}</b>",
        ["lang_selected"] = "✅ تم ضبط لغة البوت على: <b>العربية</b> 🇸🇦",
        ["bot_paired"] = "✅ <b>تم ربط البوت بنجاح</b>\n🖥 الجهاز: <b>{0}</b>\n💬 Chat ID: <code>{1}</code>",
        ["bot_unauthorized"] = "⛔ ليس لديك صلاحية للوصول إلى هذا البوت.",
        ["bot_help"] = "ℹ️ أرسل <code>/start</code> لربط هذا الحساب بالجهاز، أو ضع Chat ID التالي في البرنامج:\n<code>{0}</code>",

        ["status_header"] = "🖥 <b>حالة الجهاز: {0}</b>\n🟢 الاتصال: <b>شغّال ومتصل</b>\n⏱ مدة تشغيل الويندوز: <b>{1}</b>\n🛡 مدة عمل الخدمة: <b>{2}</b>",
        ["status_no_active"] = "\n\n💤 <i>لا توجد جلسات مستخدمين نشطة حالياً.</i>",
        ["status_active_users"] = "\n\n👤 <b>الجلسات النشطة الآن:</b>",
        ["status_user_line"] = "\n• <b>{0}</b> (Session #{1})\n   {2} | المستخدم اليوم: <b>{3}/{4} د</b>\n   المتبقي للجلسة: <b>{5} د</b> (حتى {6})",
        ["status_unrestricted"] = "حساب غير مقيّد",
        ["status_restricted"] = "مقيد 🔒",

        ["photo_caption"] = "📸 لقطة شاشة مباشرة — {0} ({1})\n👤 المستخدم: <b>{2}</b>",
        ["photo_no_session"] = "⚠️ لا توجد جلسة سطح مكتب تفاعلية نشطة حالياً لالتقاط الشاشة.",
        ["photo_failed"] = "❌ تعذر التقاط الشاشة: {0}",

        ["notify_login"] = "🟢 <b>تسجيل دخول:</b> <code>{0}</code> على جهاز <b>{1}</b>",
        ["notify_logout"] = "🚪 <b>تسجيل خروج:</b> <code>{0}</code> على جهاز <b>{1}</b>",
        ["notify_shutdown"] = "🔴 <b>إيقاف تشغيل الجهاز:</b> تم إيقاف أو إعادة تشغيل جهاز <b>{0}</b>",
        ["notify_sleep"] = "🌙 <b>وضع السكون:</b> دخل جهاز <b>{0}</b> في وضع السكون",
        ["notify_wake"] = "☀️ <b>استئناف العمل:</b> تم تنشيط جهاز <b>{0}</b> من وضع السكون",
        ["notify_limit_reached"] = "⌛ <b>انتهاء الوقت المسموح:</b> وصل المستخدم <code>{0}</code> للحد اليومي على <b>{1}</b>",
        ["notify_clock_tamper"] = "⚠️ <b>تم رصد تلاعب بالساعة:</b> المستخدم <code>{0}</code> رجع الوقت للخلف على <b>{1}</b>",
        ["notify_app_blocked"] = "🚫 <b>حظر تطبيق:</b> حاول <code>{0}</code> تشغيل <code>{1}</code> على <b>{2}</b> وتم إغلاقه",
        ["notify_time_request"] = "🙋 <b>طلب وقت إضافي</b>\n👤 الطفل: <code>{0}</code>\n🖥 الجهاز: <b>{1}</b>\n⏱ يطلب: <b>+{2} دقيقة</b>",

        ["offline_applied_header"] = "⚡ <b>تم تطبيق التعديلات التي أُرسلت أثناء إيقاف الجهاز:</b>\n🖥 الجهاز الآن <b>شغّال ومتصل</b>\n",
        ["offline_cmd_item"] = "• {0}\n",

        ["update_available"] = "🔄 <b>تحديث جديد متاح: {0}</b>\nجاري تنزيل التحديث وتثبيته في الخلفية بصمت...",
        ["update_success"] = "✅ <b>تم التحديث بنجاح إلى الإصدار {0}</b>\nيعمل النظام الآن بأحدث نسخة.",
        ["update_no_new"] = "✅ النظام يعمل بالفعل بأحدث إصدار متاح ({0}).",
        ["update_check_failed"] = "⚠️ تعذر فحص التحديثات: {0}",

        ["cmd_locked"] = "🔒 تم قفل جلسة <b>{0}</b> بنجاح.",
        ["cmd_logged_out"] = "🚪 تم تسجيل خروج <b>{0}</b> بنجاح.",
        ["cmd_time_added"] = "⏱ تم إضافة <b>{0} دقيقة</b> للمستخدم <b>{1}</b>.",
        ["cmd_time_subtracted"] = "⏱ تم إنقاص <b>{0} دقيقة</b> من المستخدم <b>{1}</b>.",
        ["cmd_time_set"] = "⏱ تم تعديل الحد اليومي للمستخدم <b>{0}</b> إلى <b>{1} دقيقة</b>.",
        ["cmd_app_blocked"] = "🚫 تم حظر التطبيق <code>{0}</code> للمستخدم <b>{1}</b>.",
        ["cmd_app_unblocked"] = "✅ تم إلغاء حظر التطبيق <code>{0}</code> للمستخدم <b>{1}</b>."
    };

    private static readonly Dictionary<string, string> English = new()
    {
        // ═══ Application Shell & Headers ═══
        ["app_title"] = "Windows Parental Control",
        ["app_subtitle"] = "Screen Time Management & Child Safety",
        ["btn_settings"] = "Settings & Telegram",
        ["btn_back"] = "Back to Dashboard",
        ["service_active"] = "Service: Running",
        ["service_down"] = "Service: Stopped",
        ["telegram_active"] = "Telegram: Connected",
        ["telegram_pairing"] = "Telegram: Pairing",
        ["telegram_off"] = "Telegram: Disabled",

        // ═══ Dashboard ═══
        ["std_users"] = "Standard Users",
        ["admin_users"] = "Administrator Users",
        ["admin_users_hint"] = "Limits cannot be placed on Administrator users because they have permissions to remove them. If you want to add limits to one of these users, change the account type to Standard first.",
        ["recent_events"] = "Recent Events",
        ["col_timestamp"] = "Timestamp",
        ["col_username"] = "Username",
        ["col_sid"] = "User SID",
        ["col_event"] = "Event Type",
        ["col_details"] = "Details",
        ["tooltip_refresh_users"] = "Refresh user list",
        ["tooltip_refresh_events"] = "Refresh event log",
        ["summary_no_limit"] = "No time limits",
        ["summary_limit_format"] = "{0} min / day ({1} remaining)",
        ["summary_no_sched"] = "No schedule",
        ["summary_sched_format"] = "{0} to {1}",
        ["summary_no_restrictions"] = "No Windows policies",
        ["summary_restrictions_count"] = "{0} Windows policies enabled",
        ["admin_role_text"] = "Administrator",
        ["std_role_text"] = "Standard User",

        // ═══ User Details ═══
        ["user_details_header"] = "Account Details & Restrictions",
        ["lbl_username"] = "USERNAME",
        ["lbl_sid"] = "SID",
        ["lbl_account_type"] = "ACCOUNT TYPE",
        ["lbl_restricted"] = "RESTRICTED",
        ["lbl_today_usage"] = "TODAY'S USAGE",
        ["btn_set"] = "Set",
        ["btn_reset_zero"] = "Reset (0)",
        ["unit_minutes"] = "min",
        ["val_yes"] = "Yes",
        ["val_no"] = "No",

        ["sec_time_limits"] = "Time Limits & Allowed Schedule",
        ["chk_enable_limits"] = "Enable time limits for this user",
        ["lbl_daily_limit"] = "Daily allowed limit (minutes):",
        ["lbl_sched_start"] = "Allowed schedule start:",
        ["lbl_sched_end"] = "Allowed schedule end:",
        ["btn_save_time"] = "Save Time Limits",

        ["sec_win_restrictions"] = "Windows Policy Restrictions",
        ["chk_lock_taskbar"] = "Lock taskbar and context menus",
        ["chk_block_settings"] = "Block Settings and Control Panel",
        ["chk_block_taskmgr"] = "Block Task Manager (Ctrl+Shift+Esc)",
        ["chk_block_cmd"] = "Block Command Prompt (CMD)",
        ["btn_save_restrictions"] = "Save Windows Policies",

        ["sec_blocked_apps"] = "Blocked Applications & Games",
        ["hint_app_name"] = "Process executable name (e.g. robloxplayerbeta or steam)",
        ["btn_add_app"] = "Block App",
        ["btn_remove_app"] = "Unblock",
        ["msg_no_blocked_apps"] = "No applications are currently blocked for this user.",

        // ═══ Settings View ═══
        ["sec_language"] = "Language & Localization",
        ["lbl_app_lang"] = "Admin Application Language:",
        ["lbl_bot_lang"] = "Telegram Bot Language:",
        ["lang_ar_display"] = "العربية",
        ["lang_en_display"] = "English",
        ["lang_fr_display"] = "Français",
        ["btn_save_language"] = "Save Language Settings",

        ["sec_telegram"] = "Telegram Bot Remote Control",
        ["chk_enable_telegram"] = "Enable Telegram Bot integration",
        ["hint_bot_token"] = "Telegram Bot Token or full API URL",
        ["hint_chat_id"] = "Parent Chat ID (leave empty, send /start to bot, then click Detect)",
        ["btn_detect_chat"] = "Detect Chat ID",
        ["chk_notify_login"] = "Send instant notification when a user signs in",
        ["chk_notify_logout"] = "Send instant notification when a user logs off",
        ["chk_notify_shutdown"] = "Send instant notification when device shuts down or restarts",
        ["chk_notify_limits"] = "Send alert on limit reached, clock tampering, or blocked app",
        ["offline_help_text"] = "Offline Queue Support: Commands sent while the PC is turned off will be queued and applied automatically when the PC boots.",
        ["btn_test_connect"] = "Test & Connect Bot",
        ["btn_save_settings"] = "Save Settings",

        ["sec_autoupdate"] = "Automatic Silent Updates",
        ["chk_enable_autoupdate"] = "Enable silent background updates from GitHub Releases",
        ["autoupdate_description"] = "The background service checks for new releases and downloads and installs them silently without interrupting users.",
        ["lbl_current_version"] = "Current Version:",
        ["btn_check_updates_now"] = "Check for Updates Now",

        ["sec_overlay"] = "Child Floating Countdown Box",
        ["chk_show_overlay"] = "Show floating remaining-time box on child's desktop",
        ["overlay_description"] = "Displays a small, draggable countdown card showing remaining time with a button to request extra time.",
        ["btn_save_overlay"] = "Save Overlay Setting",

        // ═══ Bot Menus & Buttons ═══
        ["btn_status"] = "📊 Device Status",
        ["btn_screenshot"] = "📸 Screenshot",
        ["btn_top_activity"] = "📊 Top Apps & Sites",
        ["btn_dinner_pause"] = "⏸️ Dinner Pause (30m)",
        ["btn_send_msg"] = "💬 Screen Message",
        ["btn_family_dns"] = "🛡️ Family Safe DNS",
        ["btn_time_limits"] = "⏱ Time & Limits",
        ["btn_win_restrictions"] = "⚙️ Windows Policies",
        ["btn_events"] = "📋 Recent Events",
        ["btn_language"] = "🌐 Language",
        ["btn_refresh"] = "🔄 Refresh",
        ["btn_main_menu"] = "🏠 Main Menu",
        ["btn_lock"] = "🖥 Lock ({0})",
        ["btn_logout"] = "🚪 Log off ({0})",
        ["btn_adjust_time"] = "⏱ Adjust Time ({0})",
        ["btn_approve_15"] = "✅ Approve (+15m)",
        ["btn_approve_30"] = "✅ +30m",
        ["btn_deny"] = "❌ Deny",
        ["btn_check_update"] = "🔄 Check Updates",

        // ═══ Bot Main Menu & Notifications ═══
        ["menu_title"] = "🛡️ <b>Parental Control Dashboard</b>\n🖥 Device: <b>{0}</b>",
        ["lang_selected"] = "✅ Bot language set to: <b>English</b> 🇬🇧",
        ["bot_paired"] = "✅ <b>Bot paired successfully</b>\n🖥 Device: <b>{0}</b>\n💬 Chat ID: <code>{1}</code>",
        ["bot_unauthorized"] = "⛔ You are not authorized to access this bot.",
        ["bot_help"] = "ℹ️ Send <code>/start</code> to pair this account with the PC, or set this Chat ID in the app:\n<code>{0}</code>",

        ["status_header"] = "🖥 <b>Device Status: {0}</b>\n🟢 Connection: <b>Online & Connected</b>\n⏱ Windows Uptime: <b>{1}</b>\n🛡 Service Uptime: <b>{2}</b>",
        ["status_no_active"] = "\n\n💤 <i>No active user sessions at the moment.</i>",
        ["status_active_users"] = "\n\n👤 <b>Active User Sessions:</b>",
        ["status_user_line"] = "\n• <b>{0}</b> (Session #{1})\n   {2} | Used Today: <b>{3}/{4} min</b>\n   Remaining: <b>{5} min</b> (until {6})",
        ["status_unrestricted"] = "Unrestricted",
        ["status_restricted"] = "Restricted 🔒",

        ["photo_caption"] = "📸 Live Screenshot — {0} ({1})\n👤 User: <b>{2}</b>",
        ["photo_no_session"] = "⚠️ No active interactive desktop session found for screenshot.",
        ["photo_failed"] = "❌ Failed to capture screenshot: {0}",

        ["notify_login"] = "🟢 <b>User Login:</b> <code>{0}</code> on device <b>{1}</b>",
        ["notify_logout"] = "🚪 <b>User Logoff:</b> <code>{0}</code> on device <b>{1}</b>",
        ["notify_shutdown"] = "🔴 <b>Device Shutdown:</b> PC <b>{0}</b> has been shut down or restarted",
        ["notify_sleep"] = "🌙 <b>Sleep Mode:</b> Device <b>{0}</b> went to sleep",
        ["notify_wake"] = "☀️ <b>System Resumed:</b> Device <b>{0}</b> woke up from sleep",
        ["notify_limit_reached"] = "⌛ <b>Daily Limit Reached:</b> User <code>{0}</code> reached their daily limit on <b>{1}</b>",
        ["notify_clock_tamper"] = "⚠️ <b>Clock Tamper Detected:</b> User <code>{0}</code> set the system clock backward on <b>{1}</b>",
        ["notify_app_blocked"] = "🚫 <b>Blocked App:</b> User <code>{0}</code> tried launching <code>{1}</code> on <b>{2}</b> (terminated)",
        ["notify_time_request"] = "🙋 <b>Extra Time Request</b>\n👤 Child: <code>{0}</code>\n🖥 Device: <b>{1}</b>\n⏱ Requests: <b>+{2} minutes</b>",

        ["offline_applied_header"] = "⚡ <b>Applied pending commands sent while PC was offline:</b>\n🖥 Device is now <b>Online</b>\n",
        ["offline_cmd_item"] = "• {0}\n",

        ["update_available"] = "🔄 <b>New update available: {0}</b>\nDownloading and applying update silently in the background...",
        ["update_success"] = "✅ <b>Updated successfully to version {0}</b>\nSystem is running the latest version.",
        ["update_no_new"] = "✅ System is already up to date ({0}).",
        ["update_check_failed"] = "⚠️ Failed to check for updates: {0}",

        ["cmd_locked"] = "🔒 Session for <b>{0}</b> has been locked.",
        ["cmd_logged_out"] = "🚪 <b>{0}</b> has been logged off.",
        ["cmd_time_added"] = "⏱ Added <b>{0} minutes</b> for user <b>{1}</b>.",
        ["cmd_time_subtracted"] = "⏱ Subtracted <b>{0} minutes</b> from user <b>{1}</b>.",
        ["cmd_time_set"] = "⏱ Daily limit for <b>{0}</b> set to <b>{1} minutes</b>.",
        ["cmd_app_blocked"] = "🚫 Blocked application <code>{0}</code> for user <b>{1}</b>.",
        ["cmd_app_unblocked"] = "✅ Unblocked application <code>{0}</code> for user <b>{1}</b>."
    };

    private static readonly Dictionary<string, string> French = new()
    {
        // ═══ Application Shell & Headers ═══
        ["app_title"] = "Contrôle Parental Windows",
        ["app_subtitle"] = "Gestion du temps d'écran & Protection des enfants",
        ["btn_settings"] = "Paramètres & Telegram",
        ["btn_back"] = "Retour au tableau de bord",
        ["service_active"] = "Service : En cours",
        ["service_down"] = "Service : Arrêté",
        ["telegram_active"] = "Telegram : Connecté",
        ["telegram_pairing"] = "Telegram : Association en cours",
        ["telegram_off"] = "Telegram : Désactivé",

        // ═══ Dashboard ═══
        ["std_users"] = "Utilisateurs standard",
        ["admin_users"] = "Utilisateurs administrateurs",
        ["admin_users_hint"] = "Les limites ne peuvent pas être appliquées aux administrateurs car ils peuvent les désactiver. Changez le compte en Standard pour lui appliquer des restrictions.",
        ["recent_events"] = "Derniers événements",
        ["col_timestamp"] = "Date & Heure",
        ["col_username"] = "Nom d'utilisateur",
        ["col_sid"] = "SID utilisateur",
        ["col_event"] = "Type d'événement",
        ["col_details"] = "Détails",
        ["tooltip_refresh_users"] = "Actualiser la liste des utilisateurs",
        ["tooltip_refresh_events"] = "Actualiser les événements",
        ["summary_no_limit"] = "Aucune limite de temps",
        ["summary_limit_format"] = "{0} min / jour ({1} restante{2})",
        ["summary_no_sched"] = "Aucun planning",
        ["summary_sched_format"] = "{0} à {1}",
        ["summary_no_restrictions"] = "Aucune restriction système",
        ["summary_restrictions_count"] = "{0} restrictions Windows actives",
        ["admin_role_text"] = "Administrateur",
        ["std_role_text"] = "Utilisateur standard",

        // ═══ User Details ═══
        ["user_details_header"] = "Détails du compte & Restrictions",
        ["lbl_username"] = "NOM D'UTILISATEUR",
        ["lbl_sid"] = "SID",
        ["lbl_account_type"] = "TYPE DE COMPTE",
        ["lbl_restricted"] = "RESTREINT",
        ["lbl_today_usage"] = "UTILISATION DU JOUR",
        ["btn_set"] = "Définir",
        ["btn_reset_zero"] = "Remise à zéro (0)",
        ["unit_minutes"] = "min",
        ["val_yes"] = "Oui",
        ["val_no"] = "Non",

        ["sec_time_limits"] = "Limites de temps & Planning autorisé",
        ["chk_enable_limits"] = "Activer les limites de temps pour ce compte",
        ["lbl_daily_limit"] = "Limite quotidienne autorisée (en minutes) :",
        ["lbl_sched_start"] = "Début de la plage autorisée :",
        ["lbl_sched_end"] = "Fin de la plage autorisée :",
        ["btn_save_time"] = "Enregistrer les limites",

        ["sec_win_restrictions"] = "Restrictions système Windows",
        ["chk_lock_taskbar"] = "Verrouiller la barre des tâches et menus",
        ["chk_block_settings"] = "Bloquer les Paramètres et Panneau de configuration",
        ["chk_block_taskmgr"] = "Bloquer le Gestionnaire des tâches",
        ["chk_block_cmd"] = "Bloquer l'Invite de commandes (CMD)",
        ["btn_save_restrictions"] = "Enregistrer les restrictions",

        ["sec_blocked_apps"] = "Applications et jeux bloqués",
        ["hint_app_name"] = "Nom de l'exécutable (ex. robloxplayerbeta ou steam)",
        ["btn_add_app"] = "Bloquer l'application",
        ["btn_remove_app"] = "Débloquer",
        ["msg_no_blocked_apps"] = "Aucune application n'est bloquée pour ce compte actuellement.",

        // ═══ Settings View ═══
        ["sec_language"] = "Langue & Localisation",
        ["lbl_app_lang"] = "Langue de l'application :",
        ["lbl_bot_lang"] = "Langue du bot Telegram :",
        ["lang_ar_display"] = "العربية",
        ["lang_en_display"] = "English",
        ["lang_fr_display"] = "Français",
        ["btn_save_language"] = "Enregistrer la langue",

        ["sec_telegram"] = "Contrôle à distance via Bot Telegram",
        ["chk_enable_telegram"] = "Activer l'intégration du bot Telegram",
        ["hint_bot_token"] = "Jeton du bot (Bot Token) ou URL complète de l'API",
        ["hint_chat_id"] = "Chat ID du parent (laissez vide, envoyez /start au bot puis cliquez sur Détecter)",
        ["btn_detect_chat"] = "Détecter le Chat ID",
        ["chk_notify_login"] = "Notifier lors de la connexion d'un utilisateur",
        ["chk_notify_logout"] = "Notifier lors de la déconnexion d'un utilisateur",
        ["chk_notify_shutdown"] = "Notifier lors de l'arrêt ou du redémarrage de l'appareil",
        ["chk_notify_limits"] = "Alerter si limite atteinte, modification d'horloge ou app bloquée",
        ["offline_help_text"] = "File d'attente hors-ligne : Les commandes envoyées PC éteint sont enregistrées et exécutées dès son démarrage.",
        ["btn_test_connect"] = "Tester & Associer le bot",
        ["btn_save_settings"] = "Enregistrer les paramètres",

        ["sec_autoupdate"] = "Mises à jour automatiques silencieuses",
        ["chk_enable_autoupdate"] = "Activer les mises à jour silencieuses depuis GitHub Releases",
        ["autoupdate_description"] = "Le service vérifie les nouvelles versions et les installe en arrière-plan en toute discrétion sans interrompre le travail.",
        ["lbl_current_version"] = "Version actuelle :",
        ["btn_check_updates_now"] = "Rechercher les mises à jour",

        ["sec_overlay"] = "Compte à rebours flottant enfant",
        ["chk_show_overlay"] = "Afficher la boîte flottante de temps restant sur le bureau",
        ["overlay_description"] = "Affiche une carte élégante et discrète montrant le temps restant avec un bouton pour demander du temps supplémentaire.",
        ["btn_save_overlay"] = "Enregistrer le compte à rebours",

        // ═══ Bot Menus & Buttons ═══
        ["btn_status"] = "📊 État de l'appareil",
        ["btn_screenshot"] = "📸 Capture d'écran",
        ["btn_top_activity"] = "📊 Applications & Sites",
        ["btn_dinner_pause"] = "⏸️ Pause Dîner (30m)",
        ["btn_send_msg"] = "💬 Message à l'écran",
        ["btn_family_dns"] = "🛡️ DNS Familial",
        ["btn_time_limits"] = "⏱ Temps & Limites",
        ["btn_win_restrictions"] = "⚙️ Restrictions Windows",
        ["btn_events"] = "📋 Derniers événements",
        ["btn_language"] = "🌐 Langue",
        ["btn_refresh"] = "🔄 Actualiser",
        ["btn_main_menu"] = "🏠 Menu principal",
        ["btn_lock"] = "🖥 Verrouiller ({0})",
        ["btn_logout"] = "🚪 Déconnexion ({0})",
        ["btn_adjust_time"] = "⏱ Ajuster le temps ({0})",
        ["btn_approve_15"] = "✅ Approuver (+15m)",
        ["btn_approve_30"] = "✅ +30m",
        ["btn_deny"] = "❌ Refuser",
        ["btn_check_update"] = "🔄 Mises à jour",

        // ═══ Bot Main Menu & Notifications ═══
        ["menu_title"] = "🛡️ <b>Tableau de bord du contrôle parental</b>\n🖥 Appareil : <b>{0}</b>",
        ["lang_selected"] = "✅ Langue du bot définie sur : <b>Français</b> 🇫🇷",
        ["bot_paired"] = "✅ <b>Bot associé avec succès</b>\n🖥 Appareil : <b>{0}</b>\n💬 Chat ID : <code>{1}</code>",
        ["bot_unauthorized"] = "⛔ Vous n'êtes pas autorisé à accéder à ce bot.",
        ["bot_help"] = "ℹ️ Envoyez <code>/start</code> pour associer ce compte au PC, ou définissez ce Chat ID dans l'application :\n<code>{0}</code>",

        ["status_header"] = "🖥 <b>État de l'appareil : {0}</b>\n🟢 Connexion : <b>En ligne & Connecté</b>\n⏱ Temps d'activité Windows : <b>{1}</b>\n🛡 Temps d'activité service : <b>{2}</b>",
        ["status_no_active"] = "\n\n💤 <i>Aucune session utilisateur active actuellement.</i>",
        ["status_active_users"] = "\n\n👤 <b>Sessions actives actuellement :</b>",
        ["status_user_line"] = "\n• <b>{0}</b> (Session #{1})\n   {2} | Utilisé aujourd'hui : <b>{3}/{4} min</b>\n   Restant : <b>{5} min</b> (jusqu'à {6})",
        ["status_unrestricted"] = "Non restreint",
        ["status_restricted"] = "Restreint 🔒",

        ["photo_caption"] = "📸 Capture d'écran en direct — {0} ({1})\n👤 Utilisateur : <b>{2}</b>",
        ["photo_no_session"] = "⚠️ Aucune session de bureau interactive active pour la capture d'écran.",
        ["photo_failed"] = "❌ Échec de la capture d'écran : {0}",

        ["notify_login"] = "🟢 <b>Connexion utilisateur :</b> <code>{0}</code> sur l'appareil <b>{1}</b>",
        ["notify_logout"] = "🚪 <b>Déconnexion utilisateur :</b> <code>{0}</code> sur l'appareil <b>{1}</b>",
        ["notify_shutdown"] = "🔴 <b>Arrêt de l'appareil :</b> Le PC <b>{0}</b> a été arrêté ou redémarré",
        ["notify_sleep"] = "🌙 <b>Mode veille :</b> L'appareil <b>{0}</b> est entré en veille",
        ["notify_wake"] = "☀️ <b>Reprise d'activité :</b> L'appareil <b>{0}</b> est sorti de veille",
        ["notify_limit_reached"] = "⌛ <b>Limite quotidienne atteinte :</b> <code>{0}</code> a atteint sa limite sur <b>{1}</b>",
        ["notify_clock_tamper"] = "⚠️ <b>Manipulation de l'horloge détectée :</b> <code>{0}</code> a reculé l'horloge sur <b>{1}</b>",
        ["notify_app_blocked"] = "🚫 <b>Application bloquée :</b> <code>{0}</code> a tenté de lancer <code>{1}</code> sur <b>{2}</b> (fermée)",
        ["notify_time_request"] = "🙋 <b>Demande de temps supplémentaire</b>\n👤 Enfant : <code>{0}</code>\n🖥 Appareil : <b>{1}</b>\n⏱ Demande : <b>+{2} minutes</b>",

        ["offline_applied_header"] = "⚡ <b>Commandes en attente appliquées (reçues hors-ligne) :</b>\n🖥 L'appareil est maintenant <b>En ligne</b>\n",
        ["offline_cmd_item"] = "• {0}\n",

        ["update_available"] = "🔄 <b>Nouvelle mise à jour disponible : {0}</b>\nTéléchargement et installation silencieuse en arrière-plan...",
        ["update_success"] = "✅ <b>Mise à jour réussie vers la version {0}</b>\nLe système fonctionne avec la dernière version.",
        ["update_no_new"] = "✅ Le système est déjà à jour ({0}).",
        ["update_check_failed"] = "⚠️ Échec de la vérification des mises à jour : {0}",

        ["cmd_locked"] = "🔒 La session de <b>{0}</b> a été verrouillée.",
        ["cmd_logged_out"] = "🚪 <b>{0}</b> a été déconnecté.",
        ["cmd_time_added"] = "⏱ <b>{0} minutes</b> ajoutées pour <b>{1}</b>.",
        ["cmd_time_subtracted"] = "⏱ <b>{0} minutes</b> déduites pour <b>{1}</b>.",
        ["cmd_time_set"] = "⏱ Limite quotidienne de <b>{0}</b> fixée à <b>{1} minutes</b>.",
        ["cmd_app_blocked"] = "🚫 Application <code>{0}</code> bloquée pour <b>{1}</b>.",
        ["cmd_app_unblocked"] = "✅ Application <code>{0}</code> débloquée pour <b>{1}</b>."
    };
}
