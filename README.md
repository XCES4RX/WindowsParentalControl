# نظام الرقابة الأبوية لويندوز — Windows Parental Control

نظام متكامل واحترافي للرقابة الأبوية وإدارة وقت استخدام الكمبيوتر للأطفال على نظام ويندوز، يدعم التحكم الكامل والمراقبة الحية عن بُعد عبر بوت تلغرام، التحديث التلقائي الصامت، ودعم اللغات العربية والإنجليزية والفرنسية.

---

## التحميل والتثبيت السريع

1. حمّل أحدث ملف تثبيت `ParentalControlSetup.exe` من صفحة **[الإصدارات (Releases)](https://github.com/XCES4RX/WindowsParentalControl/releases)**.
2. شغّل المثبّت كمسؤول (**Run as Administrator**) واتبع خطوات التثبيت.
3. تأكد من أن حساب الطفل على ويندوز هو حساب قياسي (**Standard User**) وليس Administrator، لأن حسابات المسؤولين لا تخضع للتقييد.

---

## لقطات من بوت تلغرام والنظام

| القائمة الرئيسية للبوت | حالة الجهاز والجلسة النشطة |
|---|---|
| ![القائمة الرئيسية لبوت تلغرام](docs/images/telegram-menu.png) | ![حالة الجهاز والجلسة النشطة](docs/images/telegram-status.png) |

| حدود الاستخدام وتعديل الوقت | طلب وقت إضافي من الطفل (موافقة / رفض) |
|---|---|
| ![حدود الاستخدام اليومي](docs/images/telegram-limits.png) | ![طلب وقت إضافي](docs/images/telegram-time-request.png) |

### سجل الأحداث الفوري عبر تلغرام
![آخر 10 أحداث](docs/images/telegram-events.jpg)

---

## المميزات الرئيسية

### 1. التحكم والإدارة عن بُعد عبر بوت تلغرام
- **لوحة تحكم ذكية:** عرض حالة الكمبيوتر، مدة التشغيل، الجلسات النشطة، والوقت المتبقي.
- **العمل في وضع عدم الاتصال (Offline Queue):** يمكنك إرسال الأوامر وتعديل الأوقات عبر البوت حتى لو كان الكمبيوتر مغلقاً (أوفلاين)؛ وفور تشغيل الجهاز وإقلاع الويندوز يقوم البوت بتطبيق كافة التعديلات المعلقة فوراً وتأكيدها.
- **تعدد اللغات (Multilingual):** دعم كامل ومنفصل للغات **العربية 🇸🇦**، **الإنجليزية 🇬🇧**، و**الفرنسية 🇫🇷**. يمكن تغيير لغة البوت فوراً عبر الأمر `/language` أو الضغط على زر `🌐 اللغة`، مع دعم كامل للاتجاه من اليمين لليسار (RTL) للغة العربية.
- **واجهة مستخدم عصرية وأنيقة:** ثيم متطور وحديث مع بطاقات بيضاء ناعمة، حدود نظيفة، وأيقونة عصرية مبسطة ومصممة بأعلى جودة.
- **أخذ لقطة شاشة مباشرة (`/screenshot`):** التقاط شاشة سطح المكتب في جلسة المستخدم الحالية وإرسالها لصورة حية للوالدين.
- **قفل الشاشة وتسجيل الخروج عن بُعد:** إمكانية قفل جلسة الطفل أو تسجيل خروجه بضغطة زر.
- **إشعارات فورية ذكية:** تنبيهات فورية عند:
  - 🟢 تسجيل دخول المستخدم.
  - 🚪 تسجيل خروج المستخدم.
  - 🔴 إيقاف تشغيل الجهاز أو إعادة تشغيله.
  - 🌙 دخول الجهاز في وضع السكون واستئناف العمل.
  - ⌛ انتهاء الوقت المسموح أو نهاية الجدول الزمني.
  - ⚠️ محاولات التلاعب بساعة الويندوز.
  - 🚫 محاولات تشغيل البرامج أو الألعاب المحظورة.
  - 🙋 طلب وقت إضافي من الطفل مع أزرار للموافقة أو الرفض.

### 2. التحديث التلقائي الصامت (Silent Auto-Updates)
- تتحقق الخدمة في الخلفية بشكل دوري من صفحة الإصدارات في GitHub.
- بمجرد رفع إصدار جديد، يتم تنزيله تلقائياً وتثبيته بصمت تام (`/VERYSILENT`) في الخلفية دون مقاطعة المستخدم أو إظهار نوافذ منبثقة، وتصل رسالة تأكيد للوالدين عبر تلغرام بنجاح التحديث.

### 3. إدارة الوقت والجدول الزمني
- **حد يومي للاستخدام:** تحديد عدد الدقائق المسموحة يومياً لكل حساب قياسي (تُحسب دقيقة بدقيقة وتُصفَّر تلقائياً عند منتصف الليل).
- **نوافذ زمنية (Schedule):** تحديد ساعات العمل المسموحة (مثلاً `07:00 - 23:00`) ومنع تسجيل الدخول خارجها.
- **تنبيه قبل انتهاء الوقت:** إظهار نافذة تحذيرية على شاشة الطفل قبل **5 دقائق** وقبل **دقيقة واحدة** من انتهاء الجلسة لحفظ أعماله.
- **كشف التلاعب بالساعة:** إذا تم إرجاع ساعة النظام للخلف، يغلق النظام الجلسة فوراً لبقية اليوم ويسجل حدث `CLOCK_TAMPER`.

### 4. مربع العدّ التنازلي العائم للطفل (`ParentalControl.Overlay`)
- مربع صغير أنيق وشفاف يطفو في زاوية الشاشة (قابل للسحب) ويعرض الوقت المتبقي الفعلي لنهاية الجلسة.
- يتغير لونه تلقائياً حسب الوقت المتبقي (أخضر ← برتقالي عند ≤ 15 دقيقة ← أحمر عند ≤ 5 دقائق).
- يحتوي على زر **`+15m`** ليرسل الطفل طلباً فورياً للوالدين عبر تلغرام للموافقة أو الرفض بضغطة زر.

### 5. قيود ويندوز لكل طفل (Windows Restrictions)
يمكن تفعيل كل قيد بشكل مستقل لكل حساب طفل من لوحة الإدارة:
- **قفل شريط المهام (`LockTaskbar`):** قفل شريط المهام ومنع إعداداته وقائمة الزر الأيمن.
- **منع الإعدادات ولوحة التحكم (`NoControlPanel`):** حظر **Windows Settings** و**Control Panel**.
- **منع مدير المهام (`DisableTaskMgr`):** تعطيل **Task Manager** (`Ctrl+Shift+Esc`).
- **منع موجه الأوامر (`DisableCMD`):** تعطيل **Command Prompt (`cmd.exe`)**.

### 6. حظر التطبيقات والألعاب (App Blocker)
- تحديد أسماء العمليات المحظورة لكل طفل (مثل `robloxplayerbeta`، `discord`، `steam`).
- مراقبة العمليات كل 5 ثوانٍ وإغلاق أي تطبيق محظور في جلسة الطفل فوراً مع إشعار الوالدين.

---

## أوامر بوت تلغرام المتاحة

| الأمر / الزر | الوظيفة |
|---|---|
| `/start` أو `/menu` | عرض القائمة الرئيسية التفاعلية |
| `/status` | حالة الجهاز، مدة التشغيل، الجلسات النشطة، والوقت المتبقي |
| `/screenshot` | التقاط لقطة شاشة حية من جلسة المستخدم النشطة وإرسالها فوراً |
| `/limits` | عرض حدود الاستخدام اليومي وفتح قائمة زيادة/إنقاص/تعديل الوقت |
| `/language` | اختيار لغة البوت (العربية 🇸🇦 / English 🇬🇧 / Français 🇫🇷) |
| `/update` | التحقق من وجود تحديث جديد على غيت هب وتثبيته فوراً |
| `/addtime <user> <±min>` | زيادة أو إنقاص الوقت مباشرة (مثال: `/addtime PC 30` أو `/addtime PC -15`) |
| `/settime <user> <min>` | تعيين الحد اليومي بالدقائق (مثال: `/settime PC 120`) |
| `/setschedule <user> <HH:mm-HH:mm>` | تعديل الجدول المسموح (مثال: `/setschedule PC 08:00-21:30`) |
| `/policies` | عرض قيود ويندوز والبرامج المحظورة لكل حساب |
| `/block <user> <app>` / `/unblock <user> <app>` | حظر أو فك حظر تطبيق معين عن بُعد |
| `/events` | عرض آخر 10 أحداث مسجلة |

---

## طريقة ربط بوت تلغرام خطوة بخطوة

1. أنشئ بوتاً جديداً عبر [@BotFather](https://t.me/BotFather) على تلغرام واحصل على **Bot Token**.
2. افتح برنامج **Parental Control** على الحاسوب واضغط **Settings & Telegram**.
3. فعّل **Enable Telegram Bot integration** والصق التوكن (أو رابط الـ API كاملاً) في خانة **Telegram Bot Token or API URL**.
4. اضغط **Test & Connect Bot**.
5. افتح محادثة البوت في تلغرام واضغط **Start (`/start`)**، ثم اضغط **Detect Chat ID** داخل البرنامج ليتم الربط تلقائياً.

---

## البنية البرمجية (Architecture)

```text
+---------------------------+       +---------------------------+
|   ParentalControl.Admin   |       |  ParentalControl.Service  |
|   (WPF Admin Dashboard)   |       |  (LocalSystem Service)    |
|  - Dashboard & UserDetail |       |  - SessionTracker         |
|  - Windows Restrictions   |       |  - UsageMonitorWorker     |
|  - Blocked Apps UI        |       |  - OverlayHostWorker      |
|  - Settings & Telegram    |       |  - TelegramBotWorker      |
|  - Multilingual & Updates |       |  - AutoUpdateWorker       |
+-------------+-------------+       +-------------+-------------+
              |                                   |
              +---------> ParentalControl.Core <--+
                            |        |
                   SQLite Database   Named Pipe IPC
        (C:\ProgramData\ParentalControl)  |
                                          v
                           +-----------------------------+
                           |  ParentalControl.Overlay    |
                           |  (User Session Countdown &  |
                           |   Desktop Screenshot Helper)|
                           +-----------------------------+
```

---

## البناء من المصدر (Build from Source)

يتطلب **.NET 8 SDK** و**Inno Setup 6**:

```powershell
.\build.ps1
```

ينتج مثبت البرنامج الكامل في: `installer\Output\ParentalControlSetup.exe`.

---

## English Overview

**Windows Parental Control** is an open-source Windows utility for managing children's screen time and computer activity. It features per-user daily limits & schedules, Windows policy restrictions (`LockTaskbar`, `NoControlPanel`, `DisableTaskMgr`, `DisableCMD`), per-user process blocking, a floating child countdown overlay (`+15m` extra time requests), live desktop screenshots, silent auto-updates from GitHub releases, offline command queue execution, and remote management via Telegram Bot with full multilingual support (Arabic, English, French).

---

## License & Credits

- MIT License — see [LICENSE](LICENSE).
- Based on WindowsParentalControl (MIT License).
