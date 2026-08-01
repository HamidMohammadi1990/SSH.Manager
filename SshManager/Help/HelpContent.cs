using SshManager.Help;

namespace SshManager.Help;

public static class HelpContent
{
    public static IReadOnlyList<HelpSection> Sections { get; } =
    [
        OverviewSection(),
        GettingStartedSection(),
        FileFormatsSection(),
        SshServerSection(),
        SshBatchSection(),
        JsonBackupSection(),
        WorkflowsSection(),
        InteractiveTokensSection(),
        SecuritySection()
    ];

    private static HelpSection OverviewSection() => new()
    {
        Id = "overview",
        Icon = "🏠",
        TitleEn = "Overview",
        TitleFa = "نمای کلی",
        Blocks =
        [
            H("What is SSH Manager?",
              "SSH Manager چیست؟"),
            P(
                "SSH Manager is a desktop tool for managing network devices over SSH and Telnet. " +
                "You can organize servers into groups, define command sequences, run them on multiple targets, " +
                "import profiles from files, and review execution results with live output and statistics.",
                "SSH Manager یک ابزار دسکتاپ برای مدیریت تجهیزات شبکه از طریق SSH و Telnet است. " +
                "می‌توانید سرورها را در گروه‌ها سازماندهی کنید، دنباله دستورات تعریف کنید، آن‌ها را روی چند هدف اجرا کنید، " +
                "پروفایل‌ها را از فایل وارد کنید و نتایج اجرا را با خروجی زنده و آمار بررسی کنید."),
            H("Main areas of the interface",
              "بخش‌های اصلی رابط کاربری"),
            B(
                [
                    "Left panel — Groups and server list, selection, Run Command, Test Connections",
                    "Center — Server editor tabs (host, credentials, targets, commands)",
                    "Right panel — Run All, Batch Jobs, Live Output, Results, Statistics",
                    "Header — Theme, Settings, Save, Export/Import, User Guide",
                    "Status bar — Current action and server count"
                ],
                [
                    "پنل چپ — گروه‌ها و لیست سرورها، انتخاب، اجرای دستور، تست اتصال",
                    "وسط — تب‌های ویرایش سرور (میزبان، اعتبارنامه، اهداف، دستورات)",
                    "پنل راست — اجرای همه، فایل‌های دسته‌ای، خروجی زنده، نتایج، آمار",
                    "هدر — تم، تنظیمات، ذخیره، خبرگیری/بازیابی، راهنما",
                    "نوار وضعیت — عملیات جاری و تعداد سرورها"
                ]),
            H("Supported file types at a glance",
              "انواع فایل پشتیبانی‌شده"),
            B(
                [
                    ".sshserver / .sshsrv — Import a single server profile (left panel → Import)",
                    ".sshbatch — Multi-target batch script (right panel → Load Batch)",
                    ".json — Full application backup (header → Export / Import)",
                    "data.json — Auto-saved app data in %AppData%\\SshManager\\ (header → Save)"
                ],
                [
                    ".sshserver / .sshsrv — وارد کردن پروفایل یک سرور (پنل چپ → Import)",
                    ".sshbatch — اسکریپت دسته‌ای چند هدف (پنل راست → Load Batch)",
                    ".json — پشتیبان کامل برنامه (هدر → Export / Import)",
                    "data.json — داده خودکار در %AppData%\\SshManager\\ (هدر → Save)"
                ])
        ]
    };

    private static HelpSection GettingStartedSection() => new()
    {
        Id = "getting-started",
        Icon = "🚀",
        TitleEn = "Getting Started",
        TitleFa = "شروع سریع",
        Blocks =
        [
            H("First-time setup",
              "راه‌اندازی اولیه"),
            B(
                [
                    "Open Settings (⚙) and set default username/password used when servers have no custom credentials",
                    "Adjust connection timeout, command timeout, and batch step delay if needed",
                    "Choose Dark or Light theme — applied immediately; click Save to persist",
                    "Click Save in the header to write servers and settings to disk"
                ],
                [
                    "تنظیمات (⚙) را باز کنید و نام کاربری/رمز پیش‌فرض را برای سرورهایی بدون اعتبارنامه اختصاصی تنظیم کنید",
                    "در صورت نیاز، زمان انتظار اتصال، زمان انتظار دستور و تأخیر بین مراحل دسته‌ای را تنظیم کنید",
                    "تم تیره یا روشن را انتخاب کنید — بلافاصله اعمال می‌شود؛ برای ماندگاری Save بزنید",
                    "در هدر Save را بزنید تا سرورها و تنظیمات روی دیسک ذخیره شوند"
                ]),
            H("Typical workflow",
              "گردش کار معمول"),
            B(
                [
                    "Create a Group (optional) to organize devices — e.g. \"Core\", \"Access\"",
                    "Add a server manually (+ Add) or import a .sshserver file",
                    "Edit the server tab: host, port, SSH/Telnet, credentials, targets, commands",
                    "Select one or more servers in the list (Ctrl/Shift for multi-select)",
                    "Click Test All Connections to verify reachability",
                    "Click Run All to execute each server's command list on its targets",
                    "Review Live Output, Results, and Statistics tabs on the right"
                ],
                [
                    "یک گروه بسازید (اختیاری) — مثلاً «Core» یا «Access»",
                    "سرور را دستی (+ Add) اضافه کنید یا فایل .sshserver وارد کنید",
                    "تب سرور را ویرایش کنید: میزبان، پورت، SSH/Telnet، اعتبارنامه، اهداف، دستورات",
                    "یک یا چند سرور را در لیست انتخاب کنید (Ctrl/Shift برای چندتایی)",
                    "Test All Connections را بزنید تا دسترسی بررسی شود",
                    "Run All را بزنید تا دستورات هر سرور روی اهدافش اجرا شود",
                    "تب‌های Live Output، Results و Statistics را در پنل راست بررسی کنید"
                ]),
            Note(
                "Unsaved changes show \"Unsaved changes\" in the status bar. Save before closing, or export a backup from the exit prompt.",
                "تغییرات ذخیره‌نشده در نوار وضعیت «Unsaved changes» را نشان می‌دهد. قبل از بستن Save بزنید یا از پیام خروج پشتیبان بگیرید.")
        ]
    };

    private static HelpSection FileFormatsSection() => new()
    {
        Id = "file-formats",
        Icon = "📁",
        TitleEn = "File Formats",
        TitleFa = "فرمت فایل‌ها",
        Blocks =
        [
            H("Shared rules for text formats",
              "قوانین مشترک فرمت‌های متنی"),
            B(
                [
                    "Encoding: UTF-8",
                    "Lines starting with # are comments",
                    "Blank lines are ignored",
                    "Sections begin with @sectionname (case-insensitive)",
                    "Content outside any section causes a parse error"
                ],
                [
                    "رمزگذاری: UTF-8",
                    "خطوطی که با # شروع می‌شوند توضیح هستند",
                    "خطوط خالی نادیده گرفته می‌شوند",
                    "بخش‌ها با @sectionname شروع می‌شوند (بدون حساسیت به حروف)",
                    "محتوای خارج از بخش‌ها خطای parse ایجاد می‌کند"
                ]),
            H("Format comparison",
              "مقایسه فرمت‌ها"),
            B(
                [
                    ".sshserver — One server saved in the app; may include credentials; each @steps = one command",
                    ".sshbatch — Temporary job; credentials NOT stored; multiple @steps blocks run sequentially per target",
                    ".json — Entire app state: settings, groups, all servers (encrypted passwords)"
                ],
                [
                    ".sshserver — یک سرور در برنامه ذخیره می‌شود؛ می‌تواند اعتبارنامه داشته باشد؛ هر @steps = یک دستور",
                    ".sshbatch — کار موقت؛ اعتبارنامه ذخیره نمی‌شود؛ چند بلوک @steps به‌ترتیب روی هر هدف اجرا می‌شود",
                    ".json — کل وضعیت برنامه: تنظیمات، گروه‌ها، همه سرورها (رمزهای رمزنگاری‌شده)"
                ])
        ]
    };

    private static HelpSection SshServerSection() => new()
    {
        Id = "sshserver",
        Icon = "🖥",
        TitleEn = "Server Profile (.sshserver)",
        TitleFa = "پروفایل سرور (.sshserver)",
        Blocks =
        [
            P(
                "Import via left panel → 📂 Import. Supported extensions: .sshserver, .sshsrv, .txt. " +
                "The file name (without extension) becomes the server name — e.g. core-switch.sshserver → \"core-switch\".",
                "از پنل چپ → 📂 Import. پسوندها: .sshserver، .sshsrv، .txt. " +
                "نام فایل (بدون پسوند) نام سرور می‌شود — مثلاً core-switch.sshserver → «core-switch»."),
            H("Sections",
              "بخش‌ها"),
            B(
                [
                    "@server — Connection info (host, port, type, description)",
                    "@credential — Username and password (stored on imported server)",
                    "@targets — Additional target IPs/hostnames for Run All",
                    "@steps — Command blocks (one block = one command; lines joined with Enter)"
                ],
                [
                    "@server — اطلاعات اتصال (میزبان، پورت، نوع، توضیح)",
                    "@credential — نام کاربری و رمز (روی سرور واردشده ذخیره می‌شود)",
                    "@targets — IP/نام میزبان اضافی برای Run All",
                    "@steps — بلوک دستور (هر بلوک = یک دستور؛ خطوط با Enter به هم وصل می‌شوند)"
                ]),
            H("@server keys",
              "کلیدهای @server"),
            B(
                [
                    "ip / host / address — Device address",
                    "port — 1–65535 (default: 22 for SSH, 23 for Telnet)",
                    "type / connectiontype — s, ssh, t, or telnet",
                    "description / desc — Free text"
                ],
                [
                    "ip / host / address — آدرس دستگاه",
                    "port — ۱ تا ۶۵۵۳۵ (پیش‌فرض: ۲۲ برای SSH، ۲۳ برای Telnet)",
                    "type / connectiontype — s، ssh، t یا telnet",
                    "description / desc — متن آزاد"
                ]),
            H("@credential keys",
              "کلیدهای @credential"),
            B(
                [
                    "user.name / username / user",
                    "user.password / password"
                ],
                [
                    "user.name / username / user",
                    "user.password / password"
                ]),
            H("Example file",
              "نمونه فایل"),
            Code(
                """
                # Server profile import example
                # File name becomes the server name

                @server
                ip=192.168.14.1
                port=22
                type=s
                description=Core switch

                @credential
                user.name=admin
                user.password=YourPasswordHere

                @targets
                192.168.105.86

                @steps
                ping 192.168.105.85
                show version

                @steps
                show ip interface brief
                """),
            Note(
                "If ip is omitted but @targets has entries, the first target becomes the host. " +
                "Duplicate server names prompt to replace. Import auto-saves to data.json.",
                "اگر ip نباشد ولی @targets پر باشد، اولین هدف میزبان می‌شود. " +
                "نام تکراری از شما تأیید جایگزینی می‌خواهد. پس از import به‌صورت خودکار در data.json ذخیره می‌شود.")
        ]
    };

    private static HelpSection SshBatchSection() => new()
    {
        Id = "sshbatch",
        Icon = "📜",
        TitleEn = "Batch Job (.sshbatch)",
        TitleFa = "فایل دسته‌ای (.sshbatch)",
        Blocks =
        [
            P(
                "Load via right panel → 📂 Load Batch. Extensions: .sshbatch, .txt. " +
                "Run with ▶ Run Batch — credentials are always requested at run time (never stored in the file).",
                "از پنل راست → 📂 Load Batch. پسوندها: .sshbatch، .txt. " +
                "با ▶ Run Batch اجرا کنید — اعتبارنامه همیشه هنگام اجرا پرسیده می‌شود (هرگز در فایل ذخیره نمی‌شود)."),
            H("Sections",
              "بخش‌ها"),
            B(
                [
                    "@defaults — Optional: type, port, delay between steps",
                    "@credential — Ignored for security (warning shown if values present)",
                    "@targets — Required: one IP/hostname per line",
                    "@steps — Required: one or more blocks; each block is one interactive step"
                ],
                [
                    "@defaults — اختیاری: type، port، delay بین مراحل",
                    "@credential — نادیده گرفته می‌شود (در صورت وجود مقدار، هشدار نمایش داده می‌شود)",
                    "@targets — اجباری: یک IP/نام میزبان در هر خط",
                    "@steps — اجباری: یک یا چند بلوک؛ هر بلوک یک مرحله تعاملی است"
                ]),
            H("@defaults keys",
              "کلیدهای @defaults"),
            B(
                [
                    "type / connectiontype — ssh or telnet",
                    "port — 1–65535",
                    "delay / stepdelay / stepdelayms — milliseconds between steps (overrides app setting)"
                ],
                [
                    "type / connectiontype — ssh یا telnet",
                    "port — ۱ تا ۶۵۵۳۵",
                    "delay / stepdelay / stepdelayms — میلی‌ثانیه بین مراحل (جایگزین تنظیم برنامه)"
                ]),
            P(
                "Protocol radio buttons (Telnet/SSH) on the right panel override the file's type and port when you run the batch.",
                "دکمه‌های رادیویی پروتکل (Telnet/SSH) در پنل راست هنگام اجرا، type و port فایل را بازنویسی می‌کنند."),
            H("Example file",
              "نمونه فایل"),
            Code(
                """
                # SSH Manager batch job example
                # Credentials are NOT stored in batch files.

                @defaults
                type=telnet
                port=23
                delay=500

                @targets
                192.168.1.2
                192.168.1.3

                @steps
                en
                <password>
                conf t

                @steps
                sh run
                wr
                <enter>
                """),
            Note(
                "Use <password> for enable/privilege mode. Use <enter> to send an extra Enter key. " +
                "If any step contains <password>, the credential dialog also asks for an enable password.",
                "از <password> برای حالت enable استفاده کنید. از <enter> برای ارسال Enter اضافی. " +
                "اگر مرحله‌ای <password> داشته باشد، دیالوگ اعتبارنامه رمز enable را هم می‌پرسد.")
        ]
    };

    private static HelpSection JsonBackupSection() => new()
    {
        Id = "json-backup",
        Icon = "💾",
        TitleEn = "JSON Backup",
        TitleFa = "پشتیبان JSON",
        Blocks =
        [
            P(
                "Header → 📤 Export creates a full backup. Header → 📥 Import replaces all current servers, groups, and settings (confirmation required).",
                "هدر → 📤 Export پشتیبان کامل می‌سازد. هدر → 📥 Import همه سرورها، گروه‌ها و تنظیمات فعلی را جایگزین می‌کند (نیاز به تأیید)."),
            H("What is included",
              "محتوای فایل"),
            B(
                [
                    "settings — Default credentials (encrypted), timeouts, batch delay, theme",
                    "groups — id, name, order",
                    "servers — Full profiles: host, port, type, group, credentials (encrypted), targets, commands"
                ],
                [
                    "settings — اعتبارنامه پیش‌فرض (رمزنگاری‌شده)، زمان‌انتظارها، تأخیر دسته‌ای، تم",
                    "groups — شناسه، نام، ترتیب",
                    "servers — پروفایل کامل: میزبان، پورت، نوع، گروه، اعتبارنامه (رمزنگاری‌شده)، اهداف، دستورات"
                ]),
            H("Auto-save location",
              "محل ذخیره خودکار"),
            P(
                "Clicking Save writes to: %AppData%\\SshManager\\data.json\n" +
                "This is separate from Export — use Export for portable backups with a timestamped filename.",
                "با Save در مسیر زیر نوشته می‌شود: %AppData%\\SshManager\\data.json\n" +
                "این با Export متفاوت است — برای پشتیبان قابل‌حمل با نام زمان‌دار از Export استفاده کنید."),
            H("Schema excerpt",
              "نمونه ساختار"),
            Code(
                """
                {
                  "settings": {
                    "defaultUsername": "admin",
                    "defaultPasswordEncrypted": "...",
                    "connectionTimeoutSeconds": 30,
                    "commandTimeoutSeconds": 60,
                    "batchStepDelayMs": 500,
                    "theme": "dark"
                  },
                  "groups": [
                    { "id": "...", "name": "Core", "order": 0 }
                  ],
                  "servers": [
                    {
                      "id": "...",
                      "name": "Router-1",
                      "host": "192.168.1.1",
                      "port": 22,
                      "connectionType": "ssh",
                      "groupId": "...",
                      "targets": ["10.0.0.1"],
                      "commands": [
                        { "id": "...", "text": "show version", "order": 0 }
                      ]
                    }
                  ]
                }
                """)
        ]
    };

    private static HelpSection WorkflowsSection() => new()
    {
        Id = "workflows",
        Icon = "⚙",
        TitleEn = "Execution Modes",
        TitleFa = "حالت‌های اجرا",
        Blocks =
        [
            H("Run All",
              "اجرای همه (Run All)"),
            B(
                [
                    "Uses selected servers that have at least one command defined",
                    "Runs each server's ordered commands on its targets (or host if targets list is empty)",
                    "Uses per-server custom credentials, or app default credentials from Settings",
                    "Best for saved, repeatable command sequences"
                ],
                [
                    "روی سرورهای انتخاب‌شده‌ای که حداقل یک دستور دارند اجرا می‌شود",
                    "دستورات مرتب هر سرور روی اهدافش (یا میزبان اگر لیست اهداف خالی باشد) اجرا می‌شود",
                    "از اعتبارنامه اختصاصی سرور یا پیش‌فرض تنظیمات استفاده می‌کند",
                    "مناسب دنباله دستورات ذخیره‌شده و تکرارپذیر"
                ]),
            H("Run Command",
              "اجرای دستور (Run Command)"),
            B(
                [
                    "Left panel → ▶ Run Command — ad-hoc execution without saving to a server profile",
                    "Enter credentials, choose SSH/Telnet, paste targets (one per line), enter commands",
                    "If servers are selected before opening, their host IPs pre-fill the targets box",
                    "Supports <enter> and <password> tokens like batch files"
                ],
                [
                    "پنل چپ → ▶ Run Command — اجرای موقت بدون ذخیره در پروفایل سرور",
                    "اعتبارنامه، SSH/Telnet، اهداف (هر خط یک IP)، دستورات را وارد کنید",
                    "اگر قبل از باز کردن سرور انتخاب شده باشد، IP میزبان‌ها در باکس اهداف پر می‌شود",
                    "توکن‌های <enter> و <password> مانند فایل دسته‌ای پشتیبانی می‌شوند"
                ]),
            H("Run Batch",
              "اجرای دسته‌ای (Run Batch)"),
            B(
                [
                    "Load a .sshbatch file, choose protocol if needed, click Run Batch",
                    "Enter username/password (and enable password if required) in the credential dialog",
                    "All targets receive all @steps blocks in order",
                    "Ideal for one-off mass configuration scripts"
                ],
                [
                    "فایل .sshbatch را بارگذاری کنید، در صورت نیاز پروتکل را انتخاب کنید و Run Batch بزنید",
                    "نام کاربری/رمز (و در صورت نیاز رمز enable) را در دیالوگ وارد کنید",
                    "همه اهداف همه بلوک‌های @steps را به‌ترتیب دریافت می‌کنند",
                    "مناسب اسکریپت‌های یک‌باره پیکربانی انبوه"
                ]),
            H("Groups & multi-select",
              "گروه‌ها و انتخاب چندتایی"),
            B(
                [
                    "Select groups to filter the server list; empty selection shows all servers",
                    "Ctrl+click or Shift+click to select multiple servers or groups",
                    "Run All, Test Connections, and Run Command use the current server selection",
                    "Right-click a group for Rename or Remove"
                ],
                [
                    "گروه انتخاب کنید تا لیست فیلتر شود؛ بدون انتخاب، همه سرورها نمایش داده می‌شوند",
                    "Ctrl+کلیک یا Shift+کلیک برای انتخاب چند سرور یا گروه",
                    "Run All، Test Connections و Run Command از انتخاب فعلی سرورها استفاده می‌کنند",
                    "راست‌کلیک روی گروه برای تغییر نام یا حذف"
                ]),
            H("After execution",
              "پس از اجرا"),
            B(
                [
                    "Live Output — Real-time log with timestamps",
                    "Results — Per-server expandable command results",
                    "Statistics — Donut charts and success rates (auto-opens when finished)",
                    "Cancel — Stops the current run (⏹ Cancel on the right panel)"
                ],
                [
                    "Live Output — گزارش لحظه‌ای با زمان",
                    "Results — نتایج دستور به تفکیک سرور",
                    "Statistics — نمودار دونات و نرخ موفقیت (پس از اتمام خودکار باز می‌شود)",
                    "Cancel — توقف اجرای جاری (⏹ Cancel در پنل راست)"
                ])
        ]
    };

    private static HelpSection InteractiveTokensSection() => new()
    {
        Id = "tokens",
        Icon = "🔤",
        TitleEn = "Special Tokens",
        TitleFa = "توکن‌های ویژه",
        Blocks =
        [
            P(
                "These tokens can appear in @steps blocks (.sshbatch, .sshserver) and in the Run Command dialog.",
                "این توکن‌ها در بلوک‌های @steps (.sshbatch، .sshserver) و دیالوگ Run Command قابل استفاده‌اند."),
            B(
                [
                    "<enter> — Sends an Enter key press (useful after prompts or confirm dialogs)",
                    "<password> — Sends the enable/privilege password (enable field in batch credential dialog, or login password if not set)"
                ],
                [
                    "<enter> — یک Enter ارسال می‌کند (مفید پس از prompt یا تأیید)",
                    "<password> — رمز enable/privilege را ارسال می‌کند (فیلد enable در دیالوگ دسته‌ای، یا رمز ورود اگر تنظیم نشده)"
                ]),
            H("SSH vs Telnet line endings",
              "پایان خط SSH در برابر Telnet"),
            P(
                "The application automatically uses the correct line ending per protocol when sending interactive steps: " +
                "SSH uses carriage return only (\\r); Telnet uses carriage return + line feed (\\r\\n). " +
                "This prevents extra Enter presses on Cisco SSH sessions.",
                "برنامه هنگام ارسال مراحل تعاملی به‌صورت خودکار پایان خط مناسب هر پروتکل را استفاده می‌کند: " +
                "SSH فقط carriage return (\\r)؛ Telnet از \\r\\n. " +
                "این از فشردن Enter اضافی در نشست SSH سیسکو جلوگیری می‌کند."),
            H("Multi-line commands",
              "دستورات چندخطی"),
            P(
                "In .sshserver files, each @steps block becomes ONE command — lines inside are sent sequentially with Enter between them. " +
                "In .sshbatch files, each @steps block is one interactive step executed on every target before moving to the next block.",
                "در .sshserver هر بلوک @steps یک دستور است — خطوط داخل آن پشت‌سرهم با Enter ارسال می‌شوند. " +
                "در .sshbatch هر بلوک @steps یک مرحله تعاملی است که روی هر هدف قبل از بلوک بعدی اجرا می‌شود.")
        ]
    };

    private static HelpSection SecuritySection() => new()
    {
        Id = "security",
        Icon = "🔒",
        TitleEn = "Security",
        TitleFa = "امنیت",
        Blocks =
        [
            B(
                [
                    "Batch files (.sshbatch) never store credentials — @credential sections are ignored",
                    "If a batch file contains credential values, you see a warning on load",
                    "Passwords in data.json and exported JSON are encrypted with Windows DPAPI",
                    "Server profile files (.sshserver) may contain plaintext passwords — handle them carefully",
                    "Use Export for backups; avoid sharing JSON or .sshserver files over insecure channels"
                ],
                [
                    "فایل‌های دسته‌ای (.sshbatch) هرگز اعتبارنامه ذخیره نمی‌کنند — بخش @credential نادیده گرفته می‌شود",
                    "اگر فایل دسته‌ای مقدار credential داشته باشد، هنگام بارگذاری هشدار می‌بینید",
                    "رمزها در data.json و JSON خبرگیری با DPAPI ویندوز رمزنگاری می‌شوند",
                    "فایل پروفایل (.sshserver) ممکن است رمز متن‌واضح داشته باشد — با احتیاط نگه دارید",
                    "برای پشتیبان از Export استفاده کنید؛ اشتراک JSON یا .sshserver از کانال ناامن خودداری کنید"
                ]),
            Note(
                "Sample files are copied to the Samples folder next to the application for reference.",
                "فایل‌های نمونه در پوشه Samples کنار برنامه برای مرجع کپی می‌شوند.")
        ]
    };

    private static HelpBlock H(string en, string fa) => new()
    {
        Kind = HelpBlockKind.Heading,
        TextEn = en,
        TextFa = fa
    };

    private static HelpBlock P(string en, string fa) => new()
    {
        Kind = HelpBlockKind.Paragraph,
        TextEn = en,
        TextFa = fa
    };

    private static HelpBlock B(IReadOnlyList<string> en, IReadOnlyList<string> fa) => new()
    {
        Kind = HelpBlockKind.Bullets,
        ItemsEn = en,
        ItemsFa = fa
    };

    private static HelpBlock Code(string text) => new()
    {
        Kind = HelpBlockKind.Code,
        TextEn = text.Trim(),
        TextFa = text.Trim()
    };

    private static HelpBlock Note(string en, string fa) => new()
    {
        Kind = HelpBlockKind.Note,
        TextEn = en,
        TextFa = fa
    };
}
