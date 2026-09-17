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
; الجولة 170 أتمّت صفحة الاعتمادات المؤجَّلة من الجولة 167: صفحة معالج
; مخصَّصة (قسم [Code] أدناه) تعرض ثلاثة شعارات (TBitmapImage) عبر نمط
; dontcopy + ExtractTemporaryFile + ExpandConstant('{tmp}\...')، ونص ملخص
; وسطري اعتماد فريق التطوير (TNewStaticText)، تظهر بعد wpWelcome وقبل
; wpSelectDir. بديل الجولة 167 المبسَّط (WizardImageFile/WizardSmallImageFile
; فقط بلا صفحة اعتمادات) لم يعد مستخدَماً؛ فتحتا صورة المعالج القياسيتان
; تبقيان لصفحتي الترحيب/الإنهاء كما كانتا.
; لا توقيع كود في هذا السكربت (لا SignTool ولا SignedUninstaller) — خارج نطاق
; هذه الجولة.

#ifndef PublishDir
  #define PublishDir "..\Sources-System-Project\bin\Release\net8.0-windows\win-x64\publish"
#endif

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{DF8A9B0D-5D38-4E06-9EB7-D7593FAF3B77}
AppMutex={{Sources-RST-2026-UNIQUE-MUTEX}
AppName=منظومة مصادر - Sources System
AppVersion={#AppVersion}
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
Source: "assets\credits_tnrc.bmp"; DestDir: "{tmp}"; Flags: dontcopy
Source: "assets\credits_app.bmp"; DestDir: "{tmp}"; Flags: dontcopy
Source: "assets\credits_designer.bmp"; DestDir: "{tmp}"; Flags: dontcopy
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\منظومة مصادر - Sources System"; Filename: "{app}\Sources.exe"
Name: "{group}\{cm:UninstallProgram,منظومة مصادر - Sources System}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\منظومة مصادر - Sources System"; Filename: "{app}\Sources.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Sources.exe"; Description: "{cm:LaunchProgram,منظومة مصادر - Sources System}"; Flags: nowait postinstall skipifsilent

; لا [UninstallDelete] عمداً — انظر التعليق أعلى الملف.

[Code]
var
  CreditsPage: TWizardPage;

procedure InitializeWizard;
var
  TnrcLogo, AppLogo, DesignerLogo: TBitmapImage;
  SummaryLabel, CreditsHeaderLabel, Dev1Label, Dev2Label: TNewStaticText;
  LogosTop, LogoSize, LogoGap: Integer;
begin
  CreditsPage := CreateCustomPage(wpWelcome, 'حول المنظومة',
    'نبذة عن منظومة مصادر وفريق التطوير');

  ExtractTemporaryFile('credits_tnrc.bmp');
  ExtractTemporaryFile('credits_app.bmp');
  ExtractTemporaryFile('credits_designer.bmp');

  LogoSize := 90;
  LogoGap := 20;
  LogosTop := 0;

  TnrcLogo := TBitmapImage.Create(CreditsPage);
  TnrcLogo.Parent := CreditsPage.Surface;
  TnrcLogo.Bitmap.LoadFromFile(ExpandConstant('{tmp}\credits_tnrc.bmp'));
  TnrcLogo.Left := 0;
  TnrcLogo.Top := LogosTop;
  TnrcLogo.Width := LogoSize;
  TnrcLogo.Height := LogoSize;

  AppLogo := TBitmapImage.Create(CreditsPage);
  AppLogo.Parent := CreditsPage.Surface;
  AppLogo.Bitmap.LoadFromFile(ExpandConstant('{tmp}\credits_app.bmp'));
  AppLogo.Left := LogoSize + LogoGap;
  AppLogo.Top := LogosTop;
  AppLogo.Width := LogoSize;
  AppLogo.Height := LogoSize;

  DesignerLogo := TBitmapImage.Create(CreditsPage);
  DesignerLogo.Parent := CreditsPage.Surface;
  DesignerLogo.Bitmap.LoadFromFile(ExpandConstant('{tmp}\credits_designer.bmp'));
  DesignerLogo.Left := (LogoSize + LogoGap) * 2;
  DesignerLogo.Top := LogosTop;
  DesignerLogo.Width := LogoSize;
  DesignerLogo.Height := LogoSize;

  SummaryLabel := TNewStaticText.Create(CreditsPage);
  SummaryLabel.Parent := CreditsPage.Surface;
  SummaryLabel.Left := 0;
  SummaryLabel.Top := LogosTop + LogoSize + 15;
  SummaryLabel.Width := CreditsPage.SurfaceWidth;
  SummaryLabel.AutoSize := False;
  SummaryLabel.WordWrap := True;
  SummaryLabel.Height := 90;
  SummaryLabel.Caption :=
    'صُمِّمَت منظومة مصادر لتوفير رقابة إشعاعية صارمة وموثوقية عالية في ' +
    'تتبع وحصر حركة المصادر والنظائر المشعة في البيئات البحثية ' +
    'والمؤسسية، بما يضمن أعلى معايير السلامة والأمان وسرعة اتخاذ ' +
    'القرار وتوثيق المعاملات الإشعاعية بدقة متناهية.';

  CreditsHeaderLabel := TNewStaticText.Create(CreditsPage);
  CreditsHeaderLabel.Parent := CreditsPage.Surface;
  CreditsHeaderLabel.Left := 0;
  CreditsHeaderLabel.Top := SummaryLabel.Top + SummaryLabel.Height + 10;
  CreditsHeaderLabel.Caption := 'التصميم والتطوير والتنفيذ:';
  CreditsHeaderLabel.Font.Style := [fsBold];

  Dev1Label := TNewStaticText.Create(CreditsPage);
  Dev1Label.Parent := CreditsPage.Surface;
  Dev1Label.Left := 0;
  Dev1Label.Top := CreditsHeaderLabel.Top + 25;
  Dev1Label.Caption := 'م. إدريس فتح الله الهري — هندسة النظم والتطوير البرمجي';

  Dev2Label := TNewStaticText.Create(CreditsPage);
  Dev2Label.Parent := CreditsPage.Surface;
  Dev2Label.Left := 0;
  Dev2Label.Top := Dev1Label.Top + 25;
  Dev2Label.Caption := 'م. رضا المريمي — التحليل الفني وإدارة المتطلبات';
end;
