# ملخص حالة المشروع — منظومة مصادر (Sources System)

**المشروع:** تطبيق WPF لتتبع المصادر المشعة، مبني على MASAR مفتوح المصدر ومُعاد تسميته بالكامل.  
**المستودع:** github.com/edrees-76/Sources-System  
**مجلد العمل:** D:\Sources-System عبر Antigravity IDE  
**وصول Claude المباشر:** مفعّل — قراءة فقط عبر موصّل antigravity-workspace على D:\Sources-System  

---

## منهجية العمل الثابتة

Claude يشخّص من الكود مباشرة ← يكتب برومبت التنفيذ في صندوق md جاهز للنسخ ← وكيل Antigravity ينفّذ ويشغّل `dotnet test` ← Claude يراجع التقرير **مقابل الكود الفعلي** لا مقابل كلام الوكيل ← commit محلي ← موافقة صريحة منفصلة قبل push ← متابعة CI حتى الأخضر.

**قواعد ثابتة:**
- ملفات `LoginWindow` / `LoginView` / `SplashWindow` لا تُمس أبداً.
- لا دمج على GitHub إلا بعد كلمة صريحة من Claude: "موافق على الدمج".
- الدليل البصري الوحيد المقبول: لقطات شاشة حقيقية من تشغيل `bin\Debug\net8.0-windows`.
- Claude يعطي **كل** البرومبتات في صندوق md مستقل وكامل، بلا إحالة لبرومبت سابق.
- أي تعديل يتجاوز نص البرومبت يجب أن يذكره الوكيل صراحة في تقريره.

---

## نظام التصميم

تدرج فاتح `#EFEAE0`←`#F7E6DC`←`#E9EEF2` — اللون الأساسي الوحيد PetroleumBlue `#1F5A66` (GoldBrush ممنوع) — ثالثي تراكوتا `#C97A4A`/`#A25C34` — ألوان الحالة: نجاح `#3FAE7A`، تحذير `#E0A93E`، خطر `#C25B4A`، معلومات `#4F7FA3` — خط Segoe UI — دعم كامل للوضع الليلي/النهاري.

**مرجع علمي وحيد لثوابت غاما:** ORNL/RSIC-45/R1 (Unger & Trubey, 1982) — 320 نظير في `gamma_constants_index.json`. ICRP 107 مرجع احتياطي عام فقط، لا يُستخدم في حسابات الجرعة.

**أدوات:** CodeRabbit مثبّت كامتداد IDE يعمل تلقائياً بعد كل commit.

---

## قرارات تصميم مثبّتة (لا تُعامَل كأخطاء)

1. **`Source.SourceCode` فهرس فريد غير مفلتر عمداً.** كود المصدر المشع معرّف دائم لجسم خاضع للرقابة، لا يُعاد استخدامه بعد الحذف الناعم، حفاظاً على سجل الحيازة والتدقيق. التعليق موجود فوقه في `OnModelCreating`. **لا تضف فلتراً.**
2. **`NeutronSource.SourceCode` و `NeutronSourceType.Code` فريدان ومفلتران** بـ `WHERE IsDeleted = 0` — إعادة الاستخدام مسموحة بقصد. الفرق بين النوعين مقصود وموثّق باختبار.
3. **`NeutronSourceTypes` و `Radioisotopes` بيانات مرجعية يزرعها `SeedData`، وملكيتها مختلفة (قرار الجولة 104):**
   - **`NeutronSourceType`:** كل حقوله للبرنامج وتُدهس في كل إقلاع، لأن قيمه معيارية بمرجع موثَّق في `StandardReference` من `ISO 8529-1:2021` و `ISO 8529-3:2023`. تعديل المستخدم لها لا يُحفظ، وهذا مقصود.
   - **`Radioisotope`:** حقول المرجع الخارجي للبرنامج وتُدهس (`Name`, `ArabicName`, `HalfLife`, `HalfLifeUnit`, `RadiationType`, `Energy`, `Yield`, `Category`, `ExemptionLimit` — وحدود الإعفاء تُؤخذ من التوصيات الدولية كما هي). أما `Notes` و `EnglishNotes` فتعليقات محلية يملكها المستخدم ولا تُدهس.
   - لا يحذف `SeedData` أي نظير أضافه المستخدم — أُلغي الحذف في الجولة 104 لأنه كان يُسقط المنظومة عند الإقلاع بقيد مفتاح خارجي.
   - كلاهما يُحفظ في إعادة الضبط للمصنع.

---

## الجولات المنجزة حديثاً (مدفوعة وCI أخضر ما لم يُذكر خلاف ذلك)

- **89** (`0c1a810`) — إصلاح القبول الصامت في `ScientificNotationParser`: `"11,000x2"` كان يُحسب 22 بدل 22000. + إنشاء `docs/schema-drift-report.md` بـ 12 بند انحراف.
- **90** (`449fb3e`) — **الأهم:** توحيد آلية المخطط على EF Migrations. حُذفت `MigrateSchema()` بالكامل (مئات أسطر SQL خام داخل `catch {}` صامت)، و`EnsureCreated()` استُبدل بـ `Database.Migrate()`، وولِّدت `InitialSchema` نظيفة، وحُمِيت `RestoreBackup` من النسخ غير المتوافقة عبر فحص `__EFMigrationsHistory`، و`SqliteInMemoryFixture` صار يستخدم `Migrate()` فأصبحت الاختبارات تعمل على المخطط الحقيقي لأول مرة.
- **92** (`8915958`) — إكمال `SystemResetService`: كان لا يحذف `NeutronSources` و`LeakTestRecords` و`SourceCertificates`، والأخطر أن السجلات المحذوفة ناعماً كانت تنجو من إعادة الضبط (غياب `IgnoreQueryFilters()`). + حذف ملفات الشهادات من القرص **بعد** `CommitAsync()` لا قبله.
- **94** (`475d750`) — رسالة واضحة عند تكرار كود مصدر محذوف، بدل خطأ SQLite خام.
- **95** (`5be5cd6`) — توحيد `AddedBy` إلى `Guid?` في الكيانات الستة بنمط `DeletedBy`/`DeletedByUser`: مفتاح خارجي + `AddedByUser` + `[NotMapped] AddedByName`. إضافة حارس وجود المستخدم في دوال `Create` الست. الترحيل `20260901133004_UnifyAddedByToGuid`.
- **96** (`535d4db`) — تصحيح `docs/schema-drift-report.md` وإعادة صياغته كسجل قرارات للمخطط الموحد على EF Core Migrations. إغلاق الإسناد الكاذب في سجل الشهادات واستبداله بـ `"غير معروف"`. استبدال `AddedBy` بـ `AddedByName` في `NeutronSourceDetailsViewModel` وتحديث ربط XAML. إصلاح مسار التراجع `Down()` بتعطيل مؤقت للمفاتيح الخارجية مع `suppressTransaction: true`.
- **97-أ** (`9ddb16b`, `d31bf4c`) — حقول شهادة المعايرة للمصادر النيترونية: إضافة `CalibratedEmissionRate` و `EmissionCalibrationDate` و `CalibrationReference` و `AnisotropyFactor`. مواءمة البذر مع `ISO 8529-1:2021` و `ISO 8529-3:2023`. توليد الترحيل `20260901184302_AddNeutronCalibrationAndDecayFields`.
- **97-ب** (`db25479`) — محرك حساب الاضمحلال للمصادر النيترونية `INeutronDecayCalculationService` بالمعادلة $B(t) = B_0 \times \exp(-\ln(2) \times \Delta t / T_{1/2})$ مع دعم وحدتي السنوات والأيام. فصل العرض في نافذة التفاصيل إلى صفين متمايزين (المعاير والمحسوب لليوم) وتوطين رسائل الحالة بالكامل.
- **98** (`02b7a14`) — تنظيف اتساق التقارير وتصحيح تاريخ معايرة الانبعاث وتوحيد تسميات معدل الانبعاث النيتروني.
- **99** (`f0f4014`, `b7a77c0`, `7a97a2a`) — دعم إدخال حقول المعايرة للمصادر النيترونية ومنع فقدان البيانات عند التعديل في `SourcesViewModel`.
- **100** (`9ff4043`, `1cf6617`, `38593d7`) — تصحيح ارتداد `TranslationHelper.GetString` لترجع `null` عند عدم العثور على المفتاح، واستعادة المفاتيح الناقصة، وإلزام تطابق القواميس باختبارات آلية، وتنظيف تسريب الـ ViewModels في `WeakReferenceMessenger`.
- **101** (`18801b5`) — إضافة وحدات النشاط `kBq` و `MBq` و `GBq` و `TBq` إلى بذر `ActivityUnits`، وترتيب القوائم بالمقدار بدل الأبجدية. الحاسبة تحمل قائمة وحدات مستقلة عن الجدول ولم تُمس.
- **102** (`14c354c`) — عمود `DisplayOrder` على `ActivityUnit` بترحيل `AddActivityUnitDisplayOrder`، والترتيب صار بالنظام: `Bq, kBq, MBq, GBq, TBq, µCi, mCi, Ci`. البذر يُسند الترتيب في فرعَي التحديث والإضافة لترقية القواعد القائمة.
- **103** — جرد شامل للكود (قراءة وتقرير فقط).
- **104** (`2cce39e`) — إيقاف الحذف المدمّر في `SeedData`: كان يحذف كل نظير خارج قائمته، فيُسقط المنظومة عند الإقلاع بقيد مفتاح خارجي إن كان النظير مرتبطاً بمصدر. وحصر فرع التحديث في حقول البرنامج، فصارت `Notes` و `EnglishNotes` ملك المستخدم لا تُدهس.
- **105** (`d2f1a6d`) — **غير مدفوعة بعد.** عزل مسارات القاعدة وإنهاء الابتلاع الصامت لاستيراد القاعدة القديمة. ملفان جديدان: `Data/DatabasePaths.cs` مصدراً وحيداً لمسار القاعدة ومجلد النسخ الاحتياطي والمسار القديم، و `Data/LegacyDatabaseImporter.cs` للاستيراد لمرة واحدة. الاستيراد صار ذرّياً: ملفا `-wal` و `-shm` أولاً ثم ملف القاعدة عبر ملف مؤقت و `File.Move` أخيراً، فلو فشل الأخير لا تبقى قاعدة مبتورة في الوجهة وتُعاد المحاولة في الإقلاع التالي بدل هجر بيانات الـ WAL صامتة. الفشل يرمي `LegacyDatabaseImportException` برسالة عربية تحوي المسارين، تمرّ على `catch` الإقلاع القائم فتُسجَّل وتُعرَض ويُغلق البرنامج — بدل الفتح على قاعدة فارغة بلا إشارة. `OnConfiguring` صار بلا أثر على نظام الملفات عدا ضمان وجود المجلد، وكان يُستدعى مع كل إنشاء سياق عبر `IDbContextFactory`. بانيا `BackupService` وُحّدا على `DatabasePaths`. وحُذف `scratch_missing_fallback.txt` ضمن الـ commit نفسه، وكان **متعقَّباً** خلافاً لما ورد في جرد الجولة 103. 12 اختباراً جديداً.
- **105-ب** (`920f29a`) — **غير مدفوعة بعد.** إتمام توحيد مسارات AppData وإنهاء التوأم الصامت في `SettingsHelper`. `AutoBackupService.GetLatestBackupDate` و `SettingsViewModel.UpdateLastBackupInfo` كانا ما زالا يعيدان تركيب `LocalAppData\Sources\Backups` يدوياً، فوُحّدا على `DatabasePaths.BackupsDirectory`. و `SettingsHelper` كان يحمل العيب نفسه: `try { File.Copy(oldFile, SettingsFile); } catch { }` لنقل `settings.ini`؛ استُخرج إلى `MigrateLegacySettings(legacyFile, targetFile)` التي تُرجع نص تحذير ولا ترمي — فقدان التفضيلات لا يبرر منع الإقلاع — ويسجّله `App` عبر `SettingsHelper.MigrationWarning`. و `SettingsDir` صار `DatabasePaths.AppDataDirectory`. 5 اختبارات جديدة.
- **106** — جرد وتصنيف الابتلاع الصامت في كود الإنتاج (قراءة وتقرير فقط، بلا commit). النتيجة في البند 8 من البنود المفتوحة.

**آخر حالة للاختبارات:** 959 اختباراً محلياً ناجحاً (957 متوقَّعة في CI — الرقم استنتاجي ولم يُتحقق منه بعد لأن الجولتين 105 و 105-ب لم تُدفعا).  
*سبب الفارق الثابت:* ملف `Sources.Tests/TestDataGeneratorTests.cs` محاط بالكامل بـ `#if DEBUG`، ومسار CI يبني بـ `--configuration Release` في `.github/workflows/tests.yml` فيستثني الفئة كاملة باختباريها.

---

## نتائج الجرد — البنود المفتوحة

سجل البنود المكشوفة غير المعالَجة. البنود 1–6 من جرد الجولة 103، والبند 7 من الجولة 105-ب، والبندان 8 و 9 من جرد الجولة 106:

1. **نصوص مثبتة خارج الترجمة:** 56 نصاً عربياً مثبتاً في XAML خارج `DynamicResource` + 7 أعمدة `DataGrid` بعناوين ثابتة — لا تتغير عند تبديل اللغة، فالترجمة الإنجليزية غير مكتملة فعلياً. أكثرها في `BorrowView.xaml` و `SettingsView.xaml` و `SourceDetailsWindow.xaml`.
2. **تباين ألوان الحالة:** `NeutronSource.StatusColor` يستعمل ألواناً مختلفة عن `SourceDetailsViewModel.StatusColor`.
3. **تبعثر سلاسل الحالة:** سلاسل الحالة (`InUse`, `Storage`, `Waste`, `Transfer`) مبعثرة في 45+ موضعاً بلا `enum`. ومثلها حالات الاستعارة ونتائج اختبار التسرب.
4. **ازدواج مصادر الحقيقة لوحدات الحاسبة:** `ActivityCalculatorViewModel` يحمل قائمة وحدات مكتوبة في الكود مستقلة عن جدول `ActivityUnits`، وله دالة `GetString` محلية صارت زائدة بعد الجولة 100.
5. **تكرار خالي من الفائدة:** `Source.SimpleArabicStatus` نسخة حرفية مطابقة لـ `ArabicStatus`.
6. **نقص ملء سجل التدقيق:** `AuditLog.OldValues` و `NewValues` تُملأ في 6 مواضع فقط عبر `LogWithChanges`، بينما غالبية الخدمات تستدعي `Log` بلا قيم.
7. **تكرار ثالث لمجلد AppData:** `LoggerService.cs` سطر 9 ما زال يبني `LocalAppData\Sources\Logs` يدوياً بدل `DatabasePaths.AppDataDirectory`. كُشف في الجولة 105-ب وأُجّل عمداً لأنه خارج نطاقها.
8. **الابتلاع الصامت في كود الإنتاج — 54 موضعاً مصنَّفة (جرد الجولة 106).** توسيع البحث من نمط `catch { }` وحده إلى الكتل التي تبتلع ثم تُرجع قيمة افتراضية رفع العدد من 31 إلى 54. الفرز بعد تصحيح تصنيفين: **30 مشروعاً** يبقى كما هو (تنظيف ملفات مؤقتة، ارتداد محولات XAML، استكشاف مسارات متعاقبة في `IsotopeLibraryService`، نصوص احتياطية في `ReportingService`، ومعالج الانهيار الأخير في `App`)؛ **19 يلزمه تسجيل** في `LoggerService`؛ **3 يلزمها إظهار للمستخدم** وهي `ActivityCalculatorViewModel.CopyResult` (تفشل الحافظة فيظن المستخدم أنه نسخ ويلصق قيمة قديمة)، و `FileHelper.OpenFile` (لا يحدث شيء عند النقر فيظن الزر معطلاً)، و `SystemSettingsService.GetSetting<T>` (قيمة تالفة في القاعدة ترتد صامتة إلى الافتراضي، فتُعرض عتبات السلامة المحفوظة خطأً — `LeakTestIntervalMonths` و `LeakTestWarningDaysThreshold` و `LowActivityThresholdPercent`)؛ **وموضعان نُقلا إلى جولة الأمان**. وبند سلوكي منفصل: `AutoBackupService.GetLatestBackupDate` يُرجع `null` عند تعذّر قراءة المجلد كما يُرجعها عند غياب النسخ، فتظن خدمة النسخ أنه لا توجد نسخ وتُطلق واحدة كل 30 دقيقة إلى الأبد؛ يلزمه تفريق الحالتين في توقيع الدالة لا تسجيلاً فقط. **الترتيب المعتمد للجولات:** 107 التضليل المباشر (`ActivityCalculatorViewModel` + `FileHelper`)، 108 خدمات الخلفية (`SystemSettingsService` + `AutoBackupService` + `BackupService`)، 109 (`App.xaml.cs` + `LeakTestsViewModel` + `LoginWindow.xaml.cs`)، 110 (`DashboardViewModel` + `SettingsViewModel` + `SourceDetailsViewModel`)، 111 (`SourcesViewModel` + `AlertsViewModel` + `ActivityCalculatorViewModel`).
9. **`_cache` ساكن في `SystemSettingsService`:** الحقل `private static Dictionary<string, string>? _cache` مُعرَّف `static` في خدمة مسجَّلة `Singleton`، فيتسرب بين السياقات وبين الاختبارات المتوازية. كُشف أثناء مراجعة الجولة 106.

---

## مؤجَّل عمداً بقرار

- **جولة الأمان** — تجزئة كلمات المرور، القفل بعد المحاولات الفاشلة، إنفاذ صلاحيات الأدوار. ومعها موضعا ابتلاع صامت نُقلا إليها من جرد الجولة 106: `AppDbContext.SeedData` يبتلع فشل ترقية هاش المدير من SHA256 إلى BCrypt، و `PasswordHelper.VerifyPassword` يبتلع فشل `BCrypt.Verify` ويُرجع `false` — فكل مستخدم بهاش SHA256 قديم يُقصى عن حسابه إلى الأبد بلا سجل ولا رسالة ولا مسار ترقية، والترقية موجودة للمدير وحده. ومعها كذلك `Roles.RemoveRange` الذي يحذف أي دور غير «مدير النظام» و«مستخدم».
- **حساب `H*(10)` للمركّبة المباشرة في المجال الحر** — معلّق حتى تتوفر شهادة معايرة حقيقية. القيود المعروفة: معامل التحويل متاح لنوع واحد من عشرة (`Cf-252` بـ 385)، و `AnisotropyFactor` غير مقاس لأي مصدر، وتشتت الغرفة يضيف حتى 40% لا يشمله الحساب.
- **النشاط الإشعاعي للمصدر النيتروني** — عمود قيمة ووحدة وتاريخ قياس. بمحرّماته: ألا يُستنتج معدل الانبعاث من النشاط، وألا تُعرض قيمة بلا وحدتها، وألا تُجمع قيم إلا بعد التحويل إلى البكريل.
- **توحيد `SourceCertificate.AttachedBy` إلى `Guid?`** — جولة مخطط على نمط الجولة 95.
- **شاشة الأنواع المرجعية المستقلة** — يجب أن تعرض `AmbientDoseConversionCoefficient` و `StandardReference` المضافين في الجولة 97.
- **`HelpView`** — آخر ما يُكتب بعد اكتمال البرمجة.

---

## قواعد ثابتة مستفادة

- في SQLite لا يمكن ترتيب `migrationBuilder.Sql` نسبةً إلى إعادة بناء الجداول؛ المزوّد يؤجّل إعادة البناء إلى النهاية. أي ترحيل يجمع بينهما يجب فحص سكربته المولَّد قبل الاعتماد.
- `PRAGMA foreign_keys` بلا أثر داخل معاملة؛ يلزمه `suppressTransaction: true`.
- ملكية البيانات المرجعية تُحسم بالحقل لا بالجدول: ما له مرجع خارجي يملكه البرنامج، والتعليقات المحلية يملكها المستخدم.
- `TranslationHelper.GetString` تُرجع `null` عند الفشل منذ الجولة 100، فالارتداد `?? "نص"` يعمل. **ممنوع** `?? key` أو `?? ""`.
- ممنوع أي `catch` صامت في كود الإنتاج لمعالجة عيب في حزمة الاختبارات.
- الاختبارات التي تُنشئ ViewModel مسجَّلاً في `WeakReferenceMessenger` يجب أن تتخلص منه بـ `using` وإلا تسرّب بين الاختبارات المتوازية.
- كل مسار داخل `LocalAppData\Sources` يُؤخذ من `Sources.Data.DatabasePaths` حصراً — `AppDataDirectory` و `DbPath` و `BackupsDirectory` و `LegacyDbPath`. **ممنوع** إعادة تركيب المسار بـ `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` في أي ملف آخر. أُنشئ في الجولة 105 بعد ثلاثة مواضع مكررة.
- تدرّج الفشل بحسب ما يُفقد: فقدان بيانات المستخدم (قاعدة البيانات) يوقف الإقلاع برسالة صريحة — `LegacyDatabaseImporter` يرمي؛ وفقدان التفضيلات فقط (`settings.ini`) لا يوقف الإقلاع بل يُسجَّل تحذيراً — `SettingsHelper.MigrateLegacySettings` تُرجع نصاً ولا ترمي. وفي الحالتين لا يُبتلع الفشل صامتاً.