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
        // Bot Menus & Buttons
        ["btn_status"] = "📊 حالة الجهاز",
        ["btn_screenshot"] = "📸 لقطة شاشة",
        ["btn_time_limits"] = "⏱ الوقت والحدود",
        ["btn_win_restrictions"] = "⚙️ قيود ويندوز",
        ["btn_events"] = "📋 آخر الأحداث",
        ["btn_language"] = "🌐 اللغة / Language",
        ["btn_refresh"] = "🔄 تحديث",
        ["btn_main_menu"] = "🏠 القائمة الرئيسية",
        ["btn_lock"] = "🖥 قفل ({0})",
        ["btn_logout"] = "🚪 خروج ({0})",
        ["btn_adjust_time"] = "⏱ تعديل الوقت ({0})",
        ["btn_approve_15"] = "✅ موافقة (+15 د)",
        ["btn_approve_30"] = "✅ +30 د",
        ["btn_deny"] = "❌ رفض",
        ["btn_check_update"] = "🔄 فحص التحديثات",

        // Main Menu Title
        ["menu_title"] = "🛡️ <b>لوحة تحكم الرقابة الأبوية</b>\n🖥 الجهاز: <b>{0}</b>",
        ["lang_selected"] = "✅ تم ضبط لغة البوت على: <b>العربية</b> 🇸🇦",

        // Pair & Auth
        ["bot_paired"] = "✅ <b>تم ربط البوت بنجاح</b>\n🖥 الجهاز: <b>{0}</b>\n💬 Chat ID: <code>{1}</code>",
        ["bot_unauthorized"] = "⛔ ليس لديك صلاحية للوصول إلى هذا البوت.",
        ["bot_help"] = "ℹ️ أرسل <code>/start</code> لربط هذا الحساب بالجهاز، أو ضع Chat ID التالي في البرنامج:\n<code>{0}</code>",

        // Status
        ["status_header"] = "🖥 <b>حالة الجهاز: {0}</b>\n🟢 الاتصال: <b>شغّال ومتصل</b>\n⏱ مدة تشغيل الويندوز: <b>{1}</b>\n🛡 مدة عمل الخدمة: <b>{2}</b>",
        ["status_no_active"] = "\n\n💤 <i>لا توجد جلسات مستخدمين نشطة حالياً.</i>",
        ["status_active_users"] = "\n\n👤 <b>الجلسات النشطة الآن:</b>",
        ["status_user_line"] = "\n• <b>{0}</b> (Session #{1})\n   {2} | المستخدم اليوم: <b>{3}/{4} د</b>\n   المتبقي للجلسة: <b>{5} د</b> (حتى {6})",
        ["status_unrestricted"] = "حساب غير مقيّد",
        ["status_restricted"] = "مقيد 🔒",

        // Screenshot
        ["photo_caption"] = "📸 لقطة شاشة مباشرة — {0} ({1})\n👤 المستخدم: <b>{2}</b>",
        ["photo_no_session"] = "⚠️ لا توجد جلسة سطح مكتب تفاعلية نشطة حالياً لالتقاط الشاشة.",
        ["photo_failed"] = "❌ تعذر التقاط الشاشة: {0}",

        // Notifications
        ["notify_login"] = "🟢 <b>تسجيل دخول:</b> <code>{0}</code> على جهاز <b>{1}</b>",
        ["notify_logout"] = "🚪 <b>تسجيل خروج:</b> <code>{0}</code> على جهاز <b>{1}</b>",
        ["notify_shutdown"] = "🔴 <b>إيقاف تشغيل الجهاز:</b> تم إيقاف أو إعادة تشغيل جهاز <b>{0}</b>",
        ["notify_sleep"] = "🌙 <b>وضع السكون:</b> دخل جهاز <b>{0}</b> في وضع السكون",
        ["notify_wake"] = "☀️ <b>استئناف العمل:</b> تم تنشيط جهاز <b>{0}</b> من وضع السكون",
        ["notify_limit_reached"] = "⌛ <b>انتهاء الوقت المسموح:</b> وصل المستخدم <code>{0}</code> للحد اليومي على <b>{1}</b>",
        ["notify_clock_tamper"] = "⚠️ <b>تم رصد تلاعب بالساعة:</b> المستخدم <code>{0}</code> رجع الوقت للخلف على <b>{1}</b>",
        ["notify_app_blocked"] = "🚫 <b>حظر تطبيق:</b> حاول <code>{0}</code> تشغيل <code>{1}</code> على <b>{2}</b> وتم إغلاقه",
        ["notify_time_request"] = "🙋 <b>طلب وقت إضافي</b>\n👤 الطفل: <code>{0}</code>\n🖥 الجهاز: <b>{1}</b>\n⏱ يطلب: <b>+{2} دقيقة</b>",

        // Offline Catchup
        ["offline_applied_header"] = "⚡ <b>تم تطبيق التعديلات التي أُرسلت أثناء إيقاف الجهاز:</b>\n🖥 الجهاز الآن <b>شغّال ومتصل</b>\n",
        ["offline_cmd_item"] = "• {0}\n",

        // Auto-Update
        ["update_available"] = "🔄 <b>تحديث جديد متاح: {0}</b>\nجاري تنزيل التحديث وتثبيته في الخلفية بصمت...",
        ["update_success"] = "✅ <b>تم التحديث بنجاح إلى الإصدار {0}</b>\nيعمل النظام الآن بأحدث نسخة.",
        ["update_no_new"] = "✅ النظام يعمل بالفعل بأحدث إصدار متاح ({0}).",
        ["update_check_failed"] = "⚠️ تعذر فحص التحديثات: {0}",

        // Actions Feedback
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
        // Bot Menus & Buttons
        ["btn_status"] = "📊 Device Status",
        ["btn_screenshot"] = "📸 Screenshot",
        ["btn_time_limits"] = "⏱ Time & Limits",
        ["btn_win_restrictions"] = "⚙️ Windows Policies",
        ["btn_events"] = "📋 Recent Events",
        ["btn_language"] = "🌐 Language / اللغة",
        ["btn_refresh"] = "🔄 Refresh",
        ["btn_main_menu"] = "🏠 Main Menu",
        ["btn_lock"] = "🖥 Lock ({0})",
        ["btn_logout"] = "🚪 Log off ({0})",
        ["btn_adjust_time"] = "⏱ Adjust Time ({0})",
        ["btn_approve_15"] = "✅ Approve (+15m)",
        ["btn_approve_30"] = "✅ +30m",
        ["btn_deny"] = "❌ Deny",
        ["btn_check_update"] = "🔄 Check Updates",

        // Main Menu Title
        ["menu_title"] = "🛡️ <b>Parental Control Dashboard</b>\n🖥 Device: <b>{0}</b>",
        ["lang_selected"] = "✅ Bot language set to: <b>English</b> 🇬🇧",

        // Pair & Auth
        ["bot_paired"] = "✅ <b>Bot paired successfully</b>\n🖥 Device: <b>{0}</b>\n💬 Chat ID: <code>{1}</code>",
        ["bot_unauthorized"] = "⛔ You are not authorized to access this bot.",
        ["bot_help"] = "ℹ️ Send <code>/start</code> to pair this account with the PC, or set this Chat ID in the app:\n<code>{0}</code>",

        // Status
        ["status_header"] = "🖥 <b>Device Status: {0}</b>\n🟢 Connection: <b>Online & Connected</b>\n⏱ Windows Uptime: <b>{1}</b>\n🛡 Service Uptime: <b>{2}</b>",
        ["status_no_active"] = "\n\n💤 <i>No active user sessions at the moment.</i>",
        ["status_active_users"] = "\n\n👤 <b>Active User Sessions:</b>",
        ["status_user_line"] = "\n• <b>{0}</b> (Session #{1})\n   {2} | Used Today: <b>{3}/{4} min</b>\n   Remaining: <b>{5} min</b> (until {6})",
        ["status_unrestricted"] = "Unrestricted",
        ["status_restricted"] = "Restricted 🔒",

        // Screenshot
        ["photo_caption"] = "📸 Live Screenshot — {0} ({1})\n👤 User: <b>{2}</b>",
        ["photo_no_session"] = "⚠️ No active interactive desktop session found for screenshot.",
        ["photo_failed"] = "❌ Failed to capture screenshot: {0}",

        // Notifications
        ["notify_login"] = "🟢 <b>User Login:</b> <code>{0}</code> on device <b>{1}</b>",
        ["notify_logout"] = "🚪 <b>User Logoff:</b> <code>{0}</code> on device <b>{1}</b>",
        ["notify_shutdown"] = "🔴 <b>Device Shutdown:</b> PC <b>{0}</b> has been shut down or restarted",
        ["notify_sleep"] = "🌙 <b>Sleep Mode:</b> Device <b>{0}</b> went to sleep",
        ["notify_wake"] = "☀️ <b>System Resumed:</b> Device <b>{0}</b> woke up from sleep",
        ["notify_limit_reached"] = "⌛ <b>Daily Limit Reached:</b> User <code>{0}</code> reached their daily limit on <b>{1}</b>",
        ["notify_clock_tamper"] = "⚠️ <b>Clock Tamper Detected:</b> User <code>{0}</code> set the system clock backward on <b>{1}</b>",
        ["notify_app_blocked"] = "🚫 <b>Blocked App:</b> User <code>{0}</code> tried launching <code>{1}</code> on <b>{2}</b> (terminated)",
        ["notify_time_request"] = "🙋 <b>Extra Time Request</b>\n👤 Child: <code>{0}</code>\n🖥 Device: <b>{1}</b>\n⏱ Requests: <b>+{2} minutes</b>",

        // Offline Catchup
        ["offline_applied_header"] = "⚡ <b>Applied pending commands sent while PC was offline:</b>\n🖥 Device is now <b>Online</b>\n",
        ["offline_cmd_item"] = "• {0}\n",

        // Auto-Update
        ["update_available"] = "🔄 <b>New update available: {0}</b>\nDownloading and applying update silently in the background...",
        ["update_success"] = "✅ <b>Updated successfully to version {0}</b>\nSystem is running the latest version.",
        ["update_no_new"] = "✅ System is already up to date ({0}).",
        ["update_check_failed"] = "⚠️ Failed to check for updates: {0}",

        // Actions Feedback
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
        // Bot Menus & Buttons
        ["btn_status"] = "📊 État de l'appareil",
        ["btn_screenshot"] = "📸 Capture d'écran",
        ["btn_time_limits"] = "⏱ Temps & Limites",
        ["btn_win_restrictions"] = "⚙️ Restrictions Windows",
        ["btn_events"] = "📋 Derniers événements",
        ["btn_language"] = "🌐 Langue / Language",
        ["btn_refresh"] = "🔄 Actualiser",
        ["btn_main_menu"] = "🏠 Menu principal",
        ["btn_lock"] = "🖥 Verrouiller ({0})",
        ["btn_logout"] = "🚪 Déconnexion ({0})",
        ["btn_adjust_time"] = "⏱ Ajuster le temps ({0})",
        ["btn_approve_15"] = "✅ Approuver (+15m)",
        ["btn_approve_30"] = "✅ +30m",
        ["btn_deny"] = "❌ Refuser",
        ["btn_check_update"] = "🔄 Mises à jour",

        // Main Menu Title
        ["menu_title"] = "🛡️ <b>Tableau de bord du contrôle parental</b>\n🖥 Appareil : <b>{0}</b>",
        ["lang_selected"] = "✅ Langue du bot définie sur : <b>Français</b> 🇫🇷",

        // Pair & Auth
        ["bot_paired"] = "✅ <b>Bot associé avec succès</b>\n🖥 Appareil : <b>{0}</b>\n💬 Chat ID : <code>{1}</code>",
        ["bot_unauthorized"] = "⛔ Vous n'êtes pas autorisé à accéder à ce bot.",
        ["bot_help"] = "ℹ️ Envoyez <code>/start</code> pour associer ce compte au PC, ou définissez ce Chat ID dans l'application :\n<code>{0}</code>",

        // Status
        ["status_header"] = "🖥 <b>État de l'appareil : {0}</b>\n🟢 Connexion : <b>En ligne & Connecté</b>\n⏱ Temps d'activité Windows : <b>{1}</b>\n🛡 Temps d'activité service : <b>{2}</b>",
        ["status_no_active"] = "\n\n💤 <i>Aucune session utilisateur active actuellement.</i>",
        ["status_active_users"] = "\n\n👤 <b>Sessions actives actuellement :</b>",
        ["status_user_line"] = "\n• <b>{0}</b> (Session #{1})\n   {2} | Utilisé aujourd'hui : <b>{3}/{4} min</b>\n   Restant : <b>{5} min</b> (jusqu'à {6})",
        ["status_unrestricted"] = "Non restreint",
        ["status_restricted"] = "Restreint 🔒",

        // Screenshot
        ["photo_caption"] = "📸 Capture d'écran en direct — {0} ({1})\n👤 Utilisateur : <b>{2}</b>",
        ["photo_no_session"] = "⚠️ Aucune session de bureau interactive active pour la capture d'écran.",
        ["photo_failed"] = "❌ Échec de la capture d'écran : {0}",

        // Notifications
        ["notify_login"] = "🟢 <b>Connexion utilisateur :</b> <code>{0}</code> sur l'appareil <b>{1}</b>",
        ["notify_logout"] = "🚪 <b>Déconnexion utilisateur :</b> <code>{0}</code> sur l'appareil <b>{1}</b>",
        ["notify_shutdown"] = "🔴 <b>Arrêt de l'appareil :</b> Le PC <b>{0}</b> a été arrêté ou redémarré",
        ["notify_sleep"] = "🌙 <b>Mode veille :</b> L'appareil <b>{0}</b> est entré en veille",
        ["notify_wake"] = "☀️ <b>Reprise d'activité :</b> L'appareil <b>{0}</b> est sorti de veille",
        ["notify_limit_reached"] = "⌛ <b>Limite quotidienne atteinte :</b> <code>{0}</code> a atteint sa limite sur <b>{1}</b>",
        ["notify_clock_tamper"] = "⚠️ <b>Manipulation de l'horloge détectée :</b> <code>{0}</code> a reculé l'horloge sur <b>{1}</b>",
        ["notify_app_blocked"] = "🚫 <b>Application bloquée :</b> <code>{0}</code> a tenté de lancer <code>{1}</code> sur <b>{2}</b> (fermée)",
        ["notify_time_request"] = "🙋 <b>Demande de temps supplémentaire</b>\n👤 Enfant : <code>{0}</code>\n🖥 Appareil : <b>{1}</b>\n⏱ Demande : <b>+{2} minutes</b>",

        // Offline Catchup
        ["offline_applied_header"] = "⚡ <b>Commandes en attente appliquées (reçues hors-ligne) :</b>\n🖥 L'appareil est maintenant <b>En ligne</b>\n",
        ["offline_cmd_item"] = "• {0}\n",

        // Auto-Update
        ["update_available"] = "🔄 <b>Nouvelle mise à jour disponible : {0}</b>\nTéléchargement et installation silencieuse en arrière-plan...",
        ["update_success"] = "✅ <b>Mise à jour réussie vers la version {0}</b>\nLe système fonctionne avec la dernière version.",
        ["update_no_new"] = "✅ Le système est déjà à jour ({0}).",
        ["update_check_failed"] = "⚠️ Échec de la vérification des mises à jour : {0}",

        // Actions Feedback
        ["cmd_locked"] = "🔒 La session de <b>{0}</b> a été verrouillée.",
        ["cmd_logged_out"] = "🚪 <b>{0}</b> a été déconnecté.",
        ["cmd_time_added"] = "⏱ <b>{0} minutes</b> ajoutées pour <b>{1}</b>.",
        ["cmd_time_subtracted"] = "⏱ <b>{0} minutes</b> déduites pour <b>{1}</b>.",
        ["cmd_time_set"] = "⏱ Limite quotidienne de <b>{0}</b> fixée à <b>{1} minutes</b>.",
        ["cmd_app_blocked"] = "🚫 Application <code>{0}</code> bloquée pour <b>{1}</b>.",
        ["cmd_app_unblocked"] = "✅ Application <code>{0}</code> débloquée pour <b>{1}</b>."
    };
}
