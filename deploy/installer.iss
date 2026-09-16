; ============================================================================
; منظومة مصادر - Sources System — سكربت Inno Setup 6
; ============================================================================
; يُبنى بواسطة deploy\build-installer.ps1 الذي يمرر {#PublishDir} (مجلد نشر
; dotnet publish الذاتي الاكتفاء win-x64) عبر /DPublishDir=...
;
; ملاحظة مقصودة (لا تُعامَل كنقص): هذا السكربت لا يتضمن قسم [UninstallDelete]
; عمداً — إلغاء التثبيت الافتراضي في Inno Setup يحذف فقط الملفات التي نسخها
; هو داخل {app} (مجلد التطبيق نفسه)، ولا يلمس إطلاقاً بيانات المستخدم في
; %LocalAppData%\Sources (قاعدة البيانات والنسخ الاحتياطية) أو
; %ProgramData%\Sources (الترخيص) لأن هذين المسارين لا يُنشآن ولا يُذكران في
; أي مكان بهذا السكربت أصلاً — فلا حاجة لاستثنائهما من حذف. هذا يحافظ على بيانات
; المستخدم كاملة عند إلغاء التثبيت أو إعادة التثبيت، بما يتوافق مع الخطوة رقم 5
; من التحقق اليدوي الإلزامي الموثَّق في docs\deployment-guide.md.
;
; بديل مبسَّط موثَّق للمعالج: استُخدمت فتحتا صورة المعالج القياسيتان في Inno
; Setup (WizardImageFile / WizardSmallImageFile) بدل صفحة اعتمادات مخصَّصة
; متعددة الشعارات عبر TBitmapImage، لأن تحميل صور من خارج [Files] في Pascal
; Script يتطلب نمط dontcopy + ExtractTemporaryFile + ExpandConstant('{tmp}\...')
; ولم يتوفر ISCC.exe في بيئة التنفيذ لاختبار هذا النمط فعلياً قبل الاعتماد
; عليه في سكربت إنتاجي — هذا البديل مُصرَّح به صراحة في عقد الجولة 167.
; لا توقيع كود في هذا السكربت (لا SignTool ولا SignedUninstaller) — خارج نطاق
; هذه الجولة.

#ifndef PublishDir
  #define PublishDir "..\Sources-System-Project\bin\Release\net8.0-windows\win-x64\publish"
#endif

[Setup]
AppId={{DF8A9B0D-5D38-4E06-9EB7-D7593FAF3B77}
AppMutex={Sources-RST-2026-UNIQUE-MUTEX}
AppName=منظومة مصادر - Sources System
AppVersion=1.0.0
AppPublisher=مركز البحوث النووية - تاجوراء
DefaultDirName={autopf}\Sources System
DefaultGroupName=منظومة مصادر - Sources System
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
WizardImageFile=assets\wizard_large.bmp
WizardSmallImageFile=assets\wizard_small.bmp
OutputBaseFilename=SourcesSystemSetup
OutputDir=output
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\Sources.exe
SetupIconFile=..\Sources-System-Project\Assets\sources_icon.ico
; لا توجد توجيهات توقيع كود (SignTool) في هذه الجولة — التوقيع خارج النطاق.

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Tasks]
Name: "desktopicon"; Description: "إنشاء أيقونة على سطح المكتب"; GroupDescription: "أيقونات إضافية:"

[Files]
; يُنسخ محتوى مجلد النشر (dotnet publish self-contained win-x64) بالكامل إلى
; {app} فقط. لا مرجع هنا إطلاقاً إلى %LocalAppData%\Sources أو
; %ProgramData%\Sources — هذان المساران بيانات مستخدم حيّة تُدار بالكامل بواسطة
; DatabasePaths داخل التطبيق نفسه، لا بواسطة المثبِّت.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\منظومة مصادر - Sources System"; Filename: "{app}\Sources.exe"
Name: "{group}\{cm:UninstallProgram,منظومة مصادر - Sources System}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\منظومة مصادر - Sources System"; Filename: "{app}\Sources.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Sources.exe"; Description: "{cm:LaunchProgram,منظومة مصادر - Sources System}"; Flags: nowait postinstall skipifsilent

; لا [UninstallDelete] عمداً — انظر التعليق أعلى الملف.
