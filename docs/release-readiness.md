# منظومة مصادر — لوحة جاهزية النشر

**آخر تحديث:** 16 سبتمبر 2026
**حالة المستودع:** الجولة 167 قيد المراجعة (Draft PR غير مدموج، فرع
`round-167-windows-installer`): أول جزء تنفيذي من ب8 (نظام النشر) — سكربتات
مصدرية لبناء مثبِّت Windows حقيقي عبر Inno Setup 6 (`deploy\installer.iss`،
`deploy\build-installer.ps1`، `deploy\assets\generate-wizard-images.ps1`،
`docs\deployment-guide.md`) + خصائص تجميعة وصفية جديدة في `AssemblyInfo.cs`.
`[Files]` ينسخ مجلد النشر فقط إلى `{app}` بلا أي مرجع لـ
`%LocalAppData%\Sources`/`%ProgramData%\Sources`، ولا `[UninstallDelete]` عمداً
لحماية بيانات المستخدم عند إلغاء التثبيت. بديل صفحة اعتمادات مبسَّط
(`WizardImageFile`/`WizardSmallImageFile`) استُخدم بدل صفحة مخصَّصة متعددة
الشعارات لتعذّر تشغيل `ISCC.exe` فعلياً في بيئة التنفيذ (حاجز عزل صدفة، لا
غياب الأداة) — مُصرَّح به صراحة في العقد. `dotnet build` صفر أخطاء، 4 تحذيرات
`CS8604` سابقة الوجود بلا علاقة بالجولة. لا `dotnet test` (لا منطق تشغيلي
تغيَّر). البناء الفعلي لـ Setup.exe والتحقق البصري الخماسي يبقيان مهمة إدريس
على جهاز Windows حقيقي. تفاصيل كاملة في §الجولة 167 أدناه. الجولة 166 قيد
المراجعة أيضاً (Draft PR غير مدموج، فرع `round-166-live-activity-recalc-in-list`):
`GetAllSources` تُعيد الآن حساب `CurrentActivityValue`
حياً من الانحلال لكل مصدر `InUse`/`Storage` بنفس نمط `GetSourceById` القائم (قراءة صرفة، بلا
`SaveChanges`)، فتستفيد `GetLowActivitySources` وقائمة المصادر ولوحة القيادة والتقارير تلقائياً بلا
أي لمس مباشر لها — 3 اختبارات جديدة، انحراف واحد موثَّق (تعديل تاريخ معايرة اختبارين قائمين خارج
وداخل قائمة الملفات المسموحة كي يعكسا انحلالاً حقيقياً بدل قيمة راكدة مضبوطة يدوياً)، 1279/1279
اختباراً محلياً (Debug)، صفر فشل. تفاصيل كاملة في §الجولة 166 أدناه. الجولة 165 قيد المراجعة أيضاً
(Draft PR غير مدموج، فرع `round-165-backup-restore-wal-safety`):
سلامة WAL في `BackupService.RestoreBackup` — نسخة الأمان الوقائية قبل الاستعادة تستخدم الآن
`PRAGMA/VACUUM INTO` بدل `File.Copy` الخام (تضمين بيانات WAL المُلتزمة غير المُدمَجة)، وحذف ملفَي
`-wal`/`-shm` القديمين فوراً بعد استبدال ملف قاعدة البيانات (في مساري ZIP والاستعادة المباشرة، وفي مسار
التراجع أيضاً) قبل إعادة فتح أي اتصال جديد — 2 اختبار جديد، انحراف واحد موثَّق (اختبار ثالث لمحاكاة قفل
ملف غير مُنفَّذ لهشاشته، مع تحقُّق يدوي بديل)، 1276/1276 اختباراً محلياً (Debug)، صفر فشل. تفاصيل كاملة في
§الجولة 165 أدناه. الجولة 164 اندمجت على `main` (PR #59). قبلها: جزآن مستقلان بالملفات نُفِّذا تسلسلياً في نفس
العقد — (أ) نقل مجلد الشهادات من مجلد التطبيق إلى `DatabasePaths.AppDataDirectory` (نفس نمط عيب الجولة 162)
مع `LegacyCertificatesImporter` جديد لاستيراد الشهادات القديمة مرة واحدة بلا حذف وبلا رمي استثناء عند الفشل،
و(ب) فرض تغيير كلمة مرور admin الافتراضية عند أول تسجيل دخول تالٍ عبر حقل `User.MustChangePassword` جديد
وحوار إلزامي `ForceChangePasswordDialog`. تم تحقُّق مستقل (`change-verifier`) وتأكيد CI أخضر (1272/1272).
الجولة 163 قيد المراجعة أيضاً (Draft PR #58 غير مدموج): إصلاح عدم استجابة 12 نافذة/عرض لتبديل
اللغة (كانت تضبط `FlowDirection="RightToLeft"` حرفياً على العنصر الجذر) + إصلاح مورد
`CurrentFlowDirection` الذي كان يستخدمه `ActivationDialog`/`PasswordPromptDialog` بلا أن يكون معرَّفاً في
أي مكان. **تصحيح لاحق على نفس الفرع/PR قبل أي دمج:** التنفيذ الأول وضع ضبط المورد داخل شرط
`if (app.MainWindow != null)` خطأً، فكان يبقى غير مضبوط طوال الجلسة الافتراضية بأكملها (عربي بلا تبديل
لغة يدوي) لأن `ApplyLanguage` تُستدعى من `OnStartup` قبل تعيين `MainWindow` ولا تُستدعى بعدها في تلك
الجلسة — اكتُشف بمراجعة القائد للملف الكامل وصُحِّح فوراً بنقل الضبط إلى خارج الشرط (تفاصيل في §الجولة
163 أدناه). الجولة 162 قيد المراجعة أيضاً (Draft PR منفصل غير مدموج): إصلاح نشر مكتبة النظائر — مسار جهاز المطوّر
المُحارَق ثابتاً كمرشَّح رابع في أربع دوال بناء مسار في `IsotopeLibraryService.cs`، وغياب نسخ
`Resources\References\` إلى مخرجات البناء في `Sources.csproj` كانا يُسقطان قائمة النظائر وأزرار PDF صامتاً
على أي جهاز غير جهاز التطوير، مكتشَف بتدقيق خارجي ومؤكَّد بفحص الكود مباشرة من القائد. الجولة 161 قيد
المراجعة أيضاً (Draft PR منفصل غير مدموج): سد ثغرة صلاحيات التحرير في مسارات الإنشاء والتعديل عبر ست خدمات
(`AuthorizationGuard.RequireEditor` لم يكن يُستدعى إلا في الحذف/الاسترجاع)، مكتشَفة بتدقيق خارجي مستقل
ومؤكَّدة بفحص الكود مباشرة. **بند مفتوح يتطلب قرار القائد (الجولة 161):** 42 حالة اختبار قديمة اعتمدت ضمنياً
على غياب الحارس؛ صُحِّحت تجهيزاتها بعد مراجعة القائد وموافقة إدريس (تفاصيل في §الجولة 161 أدناه) ·
1272/1272 نجاح محلياً (Release) بعد الجولة 164 (+17 عن الجولة 163)، صفر فشل، صفر تجاوز · تحذيرات بناء مسبقة بلا
علاقة بهذه الجولة (CS8604 في `LoginWindow.xaml.cs`/`ViewInstantiationTests.cs`) و0 أخطاء

> لوحة حالة حيّة تُحدَّث وتُصحَّح مع كل جولة. السجل التاريخي للجولات في `session-summary.md` ولا يُعدَّل.

---

## 1. سياق النشر — قرارات محسومة

| البند | القرار |
|---|---|
| عدد الأجهزة | عدة أجهزة، **قاعدة بيانات مستقلة لكل جهاز** |
| البيانات الأولية | قاعدة فارغة تماماً، إدخال يدوي من الصفر |
| طريقة التوصيل | برنامج تثبيت يُبنى بعد اكتمال التطوير |
| الفترة التجريبية | **لا توجد** — تثبيت مباشر على كل الأجهزة دفعة واحدة |
| الموعد | لا يوجد — الجودة أولاً |
| الترجمة الإنجليزية | ميزة معلَنة يجب أن تعمل بالكامل |
| الدليل | شاشة داخل البرنامج **و** ملف PDF |
| النسخ الاحتياطي | مُفعَّل افتراضياً + سؤال عن مجلد الحفظ أول مرة |
| التقارير الرقابية | الحالية تكفي — لا عمل مطلوب |
| نظام التشغيل | ويندوز 10 و11 فقط. **.NET 8 لا يدعم ويندوز 7 ولا 8.1** |

**أثر غياب الفترة التجريبية:** أي عطب يظهر عند مستخدم يظهر عند الجميع في اليوم نفسه.
**أثر القاعدة الفارغة:** لا مستخدمين بهاش SHA256 قديم، فعيب `PasswordHelper` لم يعد مانع نشر.

---

## 2. موانع النشر — بترتيب التنفيذ

### ☑ ب1 — قيم `double` غير المنتهية في مدخلات المنظومة
`NaN` و `Infinity` تعبران الحارس `<= 0` لأن `NaN <= 0` تساوي `false`. تُحفظ في قاعدة البيانات وتدخل الحسابات فيخرج رقم فاسد يُعرض ويدخل تقريراً.
النطاق: `SourcesViewModel` (الجولة 109) + إغلاق بقية المسارات ونقل الحماية الصريحة (`double.IsFinite`) لطبقة الخدمات الخمس (`RadioisotopeService`, `NeutronSourceService`, `SourceService`, `LeakTestService`, `NeutronSourceTypeService`) ومعالجات الواجهات (الجولة 110).
يُغلق قبل ب4 حتى لا ترث الحقول الجديدة العيب. **أُنجز بالكامل في الجولتين 109 و 110.**

### ☑ ب2 — إنفاذ صلاحيات الأدوار في طبقة الخدمات
مُتحقَّق منه (قبل الجولة 111): كان `SourceService` لا يسأل عن الدور في أي دالة؛ `CurrentUser` يُستعمل لتسجيل الفاعل فقط. الحماية في الواجهة وحدها.
القرار: حارس مركزي في العمليات المدمّرة والحساسة فقط — الحذف والاسترجاع وإدارة المستخدمين والأدوار. لا مراجعة شاملة.
يُضم إليه: إصلاح `Roles.RemoveRange` الذي يحذف عند كل إقلاع أي دور غير «مدير النظام» و«مستخدم».
يسبقه: تأكيد أن `LocationService` و `UserService` و `BorrowService` على النمط نفسه. **أُنجز في الجولة 111.** حارس في 17 موضعاً: سبع دوال إدارية في `UserService` بـ `RequireAdmin`، وعشر دوال حذف واسترجاع في خمس خدمات بـ `RequireEditor`. وسُدَّت ثغرة ترقية الامتياز في `ResetPassword`. **الجولة 161:** أُغلقت ثغرة الإنشاء والتعديل — `RequireEditor` أُضيف إلى 14 دالة إنشاء/تعديل عبر ست خدمات (`SourceService`، `NeutronSourceService`، `RadioisotopeService`، `LocationService`، `BorrowService`، `LeakTestService`)، مع سد ثغرة `LeakTestService` التي لم تكن محروسة بالمرة حتى في الحذف. **مُنجز بالكامل الآن** (بانتظار الدمج).

### ☑ ب3 — جودة الاختبارات المكتوبة حديثاً
ربط توكيدات `ClipboardFeedbackTests` بثوابت `ClipboardCopyHelper` وفك اعتماد اختبار النجاح على صياغة رسالة الواجهة · تشديد توكيد `BackupScanAndSettingsIntegrityTests` ليكون محدود الطرفين يرفض التواريخ المستقبلية · استخراج اقتطاع نصوص السجل الآمن إلى `LogTextHelper` بحد 50 حرفاً وحماية الأزواج البديلة مع 10 اختبارات شاملة وإعادة تسمية الاختبار السابق ليعكس فحص عدم الاستثناء · إغلاق ثغرة التاريخ المستقبلي في `AutoBackupService` برفضه لمصدري الإعدادات والمسح مع مهلة 5 دقائق لانحراف الساعة وإضافة 4 اختبارات جديدة للمسح والإعدادات · إزالة ستة اختبارات غير قابلة للفشل في مسار النسخ التلقائي وإثبات قابليتها للفشل بثلاث طفرات مؤقتة (113-ب). **أُنجز بالكامل في الجولتين 113 و 113-ب.**

### ☑ ب4 — توسعة المصادر النيترونية (86-ج)
حقول المصدر: الشركة المصنعة، الموديل، النشاط الإشعاعي وتاريخ قياسه، الكبسولة، الأبعاد.
حقول النوع المرجعي: أقصى طاقة، نوع الطيف.
شاشة مستقلة كاملة للأنواع المرجعية على نمط مكتبة النظائر + قسم المراجع العلمية.
محرَّمات حقل النشاط: ألا يُستنتج معدل الانبعاث منه، وألا تُعرض قيمة بلا وحدتها، وألا تُجمع قيم إلا بعد التحويل إلى البكريل.
يسبقه: مراجعة `P074_scr` و `TRS393`. يحتاج ترحيل مخطط.
**الجولة 123 (فرعية أولى، منجزة):** ترحيل `AddNeutronManufacturingAndPhotonRatioFields` يضيف
`NeutronSource.Manufacturer`/`Model`/`CapsuleLengthMm`/`CapsuleDiameterMm` (بترحيل الأبعاد يُشترط
`IsFinite` و`> 0`، مطابقاً لنمط `CalibratedEmissionRate`) و`NeutronSourceType.PhotonToNeutronDoseRatio`
(`IsFinite` فقط، بلا شرط `>0` لأنها نسبة بلا وحدة). خدمات `NeutronSourceService`/
`NeutronSourceTypeService` وسجلات `OldValues`/`NewValues` عُدِّلت لتشمل الحقول الجديدة.
**الجولة 124 (فرعية ثانية، منجزة):** أُغلقت ملاحظة إبهام بيانات البذر. صف `Am-241/Be` الوحيد في
`SeedData()` كان يترك `MeanNeutronEnergyMeV`/`AmbientDoseConversionCoefficient` فارغين لاعتمادهما على
حجم المصدر. استُبدل بصفين دقيقين: `Am-241/Be-Small` (`MeanNeutronEnergyMeV = 4.17`,
`AmbientDoseConversionCoefficient = 393.0`) و`Am-241/Be-Large` (`4.05` و`387.0` على الترتيب)، وفق
ISO 8529-1:2021 Table 1 (الطاقة المرجّحة بالتدفق) وISO 8529-3:2023 Table 2 (`h*_Φ(10;E)`؛ القيمة
الموحّدة السابقة 391 pSv·cm² موثّقة هناك كإصدار سابق مُستبدَل). الصف القديم "Am-241/Be"، إن وُجد من
تشغيل بذر سابق، يُبطَل بالحذف الناعم (`IsDeleted = true`, `DeletedAt`) لا يُحذف فعلياً ولا يُترك نشطاً
بجانب الصفين الجديدين — يحافظ على سلامة أي `NeutronSource` يشير إليه بمفتاح خارجي (`DeleteBehavior.
Restrict`) لكنه سيشير الآن إلى نوع محذوف ناعماً؛ لا بيانات إنتاجية حالياً فلا أثر عملي. لا ترحيل مخطط
مطلوب (`IsDeleted` موجود مسبقاً).
**الجولة 125 (فرعية ثالثة، منجزة محلياً):** أول جولة واجهة لـب4. وصل الحقول الخمسة التي أضافتها
الجولة 123 بالمخطط والخدمة (بلا واجهة حينها) بشاشتين: `SourcesView.xaml`/`SourcesViewModel.cs` صار
نموذج المصدر النيتروني يعرض ويحفظ `Manufacturer`/`Model` (بتعبئة `EditNeutronSource(...)` وإسناد
`SaveAsync()` الناقصين سابقاً) و`CapsuleLengthMm`/`CapsuleDiameterMm` (زوجا حقلين جديدان بنمط
`EditAnisotropyFactorText` بحارس عميل يرفض غير المنتهي و`<=0`، والخدمة تُحرِّس ذلك أصلاً منذ الجولة
123). و`NeutronSourceTypesWindow.xaml`/`NeutronSourceTypesViewModel.cs` صار يعرض ويحفظ
`PhotonToNeutronDoseRatio` (حارس يرفض غير المنتهي فقط، بلا `<=0`، يطابق قرار الجولة 123). **انحراف
مسجَّل:** رسائل الخطأ الثلاث الجديدة نص عربي حرفي مباشر لا `TranslationHelper.GetString` — قواميس
`Resources/Strings.*.xaml` خارج نطاق ملفات هذه الجولة المسموحة، وإضافة مفتاح غير موجود في كلا
القاموسين يُسقط اختبار `TranslationKeysTests` القائم. الترجمة الإنجليزية للتسميات الجديدة مؤجَّلة
لـب5 صراحة. 1109/1107 اختباراً (Debug/Release). **التحقق البصري الحقيقي من `bin\Debug\net8.0-windows`
لم يتم بعد — بانتظار إدريس قبل اعتبار هاتين الشاشتين مكتملتين بصرياً.**
**الجولة 126 (فرعية رابعة، منجزة محلياً):** إضافة نشاط الأمريسيوم-241 كما في شهادة المصدر —
حقلان جديدان مستقلان `NeutronSource.Am241ActivityValue` (`double?`) و`Am241ActivityUnitId`
(`Guid?`, FK اختياري على `ActivityUnit` بنمط `Restrict`، يعيد استخدام جدول `ActivityUnit` القائم
بلا جدول جديد)، بترحيل `AddNeutronSourceAm241Activity`. القرار الجوهري (ISO 8529-1:2021 §4.4):
معدل الانبعاث النيتروني المُعاير ونشاط الأمريسيوم-241 كميتان **مستقلتان لا تُشتق إحداهما من
الأخرى** — العلاقة بينهما تتأثر بعملية التصنيع ودرجة خلط المصدر ولا تخضع لصيغة عامة، فيجب تسجيل
كليهما من شهادة المصدر مباشرة. تاريخ المرجع لاضمحلال هذا النشاط هو `CalibrationDate` نفسه (قرار
معتمد مع إدريس — لا حقل تاريخ جديد). خدمة الحساب `NeutronDecayCalculationService` (خالية من أي
حقن تبعية عمداً) اكتسبت دالتين جديدتين `CalculateCurrentAm241Activity`/`CalculateAm241ActivityAtDate`
بنفس بنية `CalculateEmissionRate` القائمة تماماً: تُحوَّل القيمة إلى بكريل عبر
`Am241ActivityUnit.ConversionToBq` ثم يُطبَّق `B(t) = B₀ × exp(-ln(2) × Δt / T½)` بنصف عمر
432.2 سنة **مُكرَّر عمداً** كثابت محلي (يطابق قيمة جدول `Radioisotope` المرجعية) حفاظاً على
استقلالية هذه الآلة الحسابية عن أي حقن خدمات، بتعليق صريح يوثّق التكرار المتعمَّد. ثلاث حالات
حالة جديدة أُضيفت لتعداد `NeutronDecayCalculationStatus`: `NotRecorded` (الحقل الاختياري غير
مُدخَل — حالة طبيعية لا خطأ)، `MissingActivityUnit` (خاصية التنقل لم تُحمَّل من المستدعي، يماثل
`MissingSourceType`)، و`InvalidActivityValue` (القيمة بعد التحويل غير منتهية أو `<=0`). `NeutronSourceService.Create`/`Update`: حارس `IsFinite`+`>0` على `Am241ActivityValue` عند
إدخالها (بنمط `CapsuleLengthMm`)، ورفض صريح إن أُدخل أحد الحقلين (القيمة أو الوحدة) بمفرده دون
الآخر، والتحقق من وجود `Am241ActivityUnitId` فعلياً في جدول `ActivityUnit`. كائنات
`LogWithChanges` الخمسة في الملف (نمط الجولات 119-123) وُسِّعت لتشمل الحقلين معاً — تفادياً لإعادة
فتح فجوة التدقيق. **لا واجهة في هذه الجولة** (يتبع نمط الفصل 123→125: مخطط/حساب أولاً، وصل واجهة
لاحقاً). 15 اختباراً جديداً: تسعة في `NeutronDecayTests.cs` (تغطي غير المسجَّل، وحدة غير محمَّلة،
قيمة غير صالحة بعد التحويل، اضمحلال نصف عمر كامل، اضمحلال جزئي 100 سنة مطابق لقيمة محسوبة يدوياً،
تحويل وحدة غير-بكريل قبل تطبيق الاضمحلال، تاريخ معايرة غائب، تاريخ حساب يسبق المعايرة، مصدر
`null`)، وستة في `NeutronSourceServiceTests.cs` (رفض قيمة غير منتهية/غير موجبة، رفض إدخال أحد
الحقلين بمفرده عند الإنشاء والتعديل معاً، رفض وحدة غير موجودة، وإثبات مباشر أن سجل التدقيق
`NewValues` يحوي كلا الحقلين فعلياً عند الإنشاء والتعديل). 1124/1122 اختباراً (Debug/Release)،
5 تحذيرات بناء (CS8604 مسبقة، بلا علاقة بهذه الجولة) و0 أخطاء.
**الجولة 127 (فرعية خامسة، منجزة محلياً):** إصلاح — لا ميزة جديدة. CodeRabbit في مراجعة PR #14
(دمج الجولة 126) اكتشف أن دوال القراءة الخمس في `NeutronSourceService` (`GetAll`, `GetDeleted`,
`GetById`, `GetByCode`, `GetByLocation`) كانت جميعها تحمّل `.Include(n => n.NeutronSourceType)` و
`.Include(n => n.Location)` و`.Include(n => n.AddedByUser)` (و`.Include(n => n.DeletedByUser)` في
`GetDeleted`) دون `.Include(n => n.Am241ActivityUnit)` قط. تأكَّد الاكتشاف مباشرةً بقراءة الكود لا
بالاعتماد على ادّعاء CodeRabbit وحده. بما أن `Am241ActivityUnit` خاصية تنقل غير افتراضية
(`non-virtual`، كما أُضيفت في الجولة 126)، تبقى `null` بعد التخلص من `DbContext`، فكانت
`CalculateCurrentAm241Activity`/`CalculateAm241ActivityAtDate` تُرجعان دوماً `MissingActivityUnit`
لأي مصدر يُجلب عبر هذه الدوال الخمس — **حتى مع تخزين `Am241ActivityUnitId` بشكل صحيح فعلياً في
قاعدة البيانات**. الإصلاح إضافي بحت: `.Include(n => n.Am241ActivityUnit)` أُضيفت للدوال الخمس بنفس
موضع/أسلوب استدعاءات `.Include()` القائمة دون إعادة ترتيبها، بلا أي تغيير على منطق التصفية أو
الترتيب أو نوع الإرجاع. لا مساس بـ `Create`/`Update`/`Delete`/`Restore` في الملف نفسه (صحيحة أصلاً
ولم تُمس)، ولا بـ`NeutronDecayCalculationService` (منطق الحساب صحيح كما تحقَّق في الجولة 126؛ هذه
الجولة تسد فجوة تحميل البيانات التي تسبقه فقط)، ولا XAML أو ViewModel. اختبار انحداري واحد جديد في
`NeutronSourceServiceTests.cs`
(`GetById_WithAm241Activity_LoadsActivityUnit_AndDecayCalculationSucceeds`) يثبت الإصلاح من طرف
لطرف فعلياً لا وجود سطر `.Include()` فقط: ينشئ مصدراً نيترونياً بقيمة/وحدة نشاط أمريسيوم-241
صالحتين وتاريخ معايرة عبر `Create` الفعلية للخدمة، يجلبه ثانيةً عبر `GetById` (لا كائن مُنشأ يدوياً)،
ثم يستدعي `CalculateCurrentAm241Activity`/`CalculateAm241ActivityAtDate` على الكائن المجلوب ويؤكد
`Status == Calculated` مع `CurrentActivityBq` غير فارغ لكلتا الدالتين. 1125/1123 اختباراً
(Debug/Release، +1 عن الجولة 126)، 5 تحذيرات بناء (CS8604 مسبقة، بلا علاقة بهذه الجولة) و0 أخطاء.
**الجولة 128 (فرعية سادسة وأخيرة لـب4، منجزة محلياً):** الجولة الأخيرة لواجهة ب4. وصل
`NeutronSource.Am241ActivityValue`/`Am241ActivityUnitId` بنموذج المصدر الموحَّد بحقل قيمة + قائمة
اختيار وحدة قابلة للتغيير (بناءً على طلب إدريس الصريح — تكرار نمط `EditInitialActivity`/
`EditInitialActivityText`/`EditInitialUnitId` القائم في `Source`، لكن اختيارياً `double?` بنمط
`EditCapsuleLengthMm` من الجولة 125 لا نمط `Source` الإلزامي). `SourcesViewModel` اكتسب حقن
اختياري جديد `INeutronDecayCalculationService? neutronDecayService = null` (يفتَرِض
`new NeutronDecayCalculationService()` بنفس نمط `IDecayCalculationService?` القائم في المُنشئ
نفسه)، وخاصية عرض للقراءة فقط `DisplayAm241CurrentActivity` تُحسَب عبر
`CalculateCurrentAm241Activity(target)` عند فتح `EditNeutronSource(...)` لمصدر قائم فقط — لسجل
جديد (`IsNew == true`) تبقى فارغة عمداً إذ لا شيء يُحسَب بعد. حارس عميل «كلاهما أو لا شيء» قبل
الحفظ (بنمط حارس `EditCapsuleLengthText`) يرفض إدخال القيمة بلا وحدة أو العكس قبل استدعاء الخدمة،
والخدمة نفسها (`NeutronSourceService`) تُحرِّس ذلك أصلاً منذ الجولة 126 كخط دفاع ثانٍ. **انحراف
مسجَّل (يكرر نمط الجولة 125):** رسائل الخطأ الجديدة (حارس «كلاهما أو لا شيء» وقيمة النشاط غير
الصالحة) وحالتا العرض `MissingActivityUnit`/`InvalidActivityValue` الجديدتان ونص «لم يُسجَّل» —
نص عربي حرفي مباشر لا `TranslationHelper.GetString` بمفتاح جديد؛ قواميس `Resources/Strings.*.xaml`
خارج نطاق ملفات هذه الجولة المسموحة، وإضافة مفتاح غير موجود في كلا القاموسين يُسقط اختبار
`TranslationKeysTests` القائم (تأكَّد ذلك فعلياً بمحاولة أولى فشلت ثم أُصلحت). الترجمة الإنجليزية
مؤجَّلة لـب5 صراحة. 6 اختبارات جديدة في `SourcesViewModelTests.cs`: إنشاء بقيمة+وحدة يحفظ كليهما
فعلياً عبر `Create` المُلتقَط، تعديل مصدر بنشاط مخزَّن يُعبِّئ القيمة/الوحدة ويحسب النشاط الحالي
بعد اضمحلال نصف عمر كامل (432.2 سنة) مطابق يدوياً لنصف القيمة الابتدائية (بنمط الجولة 126
التحقّقي)، تعديل مصدر بلا نشاط مخزَّن يعرض «لم يُسجَّل» بلا رسالة خطأ، سجل جديد لا يحسب/يعرض شيئاً،
وحالتا رفض عميل (قيمة بلا وحدة / وحدة بلا قيمة) عبر نظرية بحالتين. 1131/1129 اختباراً
(Debug/Release، +6 عن الجولة 127)، 5 تحذيرات بناء (CS8604 مسبقة، بلا علاقة بهذه الجولة) و0 أخطاء.
**التحقق البصري الحقيقي من `bin\Debug\net8.0-windows` لم يتم بعد — بانتظار إدريس.**
مراجعة CodeRabbit على PR #16 (طلبت صراحة عبر `@coderabbitai review` بعد تحويل الـPR من مسودة)
اكتشفت ملاحظة فعلية واحدة قابلة للتنفيذ: احتمال حفظ `EditActivityText` (يُسمَّى `EditAm241ActivityText` وقت اكتشاف CodeRabbit، عُمِّم لاحقاً بتصحيح إدريس) بقيمة قديمة/فارغة
عند الحفظ بمفتاح Enter بسبب `UpdateSourceTrigger=LostFocus` — مُفصَّلة أعلاه في القسم 3 كدَين
تقني مؤجَّل موثَّق (يمسّ سبعة حقول مشتركة في النمط نفسه، ليس حصرياً لهذه الجولة). القرار: تأجيل
بلا إصلاح جزئي في هذه الجولة، حفاظاً على مطابقة العقد (تكرار النمط القائم حرفياً) وتفادياً لتباين
محلي بين حقل جديد وستة حقول قديمة تحمل العيب نفسه.

**تصحيح إدريس قبل الدمج (خطأ تصميم حقيقي، لا تجميلي — نُفِّذ داخل نفس PR #16 لا كجولة لاحقة):**
راجع إدريس PR #16 قبل الموافقة على الدمج واكتشف أن `Am241ActivityValue`/`Am241ActivityUnitId`
ومنطق حساب الاضمحلال مُقيَّدان بالأمريسيوم-241 تحديداً رغم أن `NeutronSource` قد يكون أياً من عشرة
أنواع مرجعية (Cf-252، Pu-238/Be، Pu-239/Be، Ra-226/Be، Sb-124/Be، Am-241/Be ...إلخ)، لكل منها
`ParentNuclide`/`HalfLife`/`HalfLifeUnit` مُسجَّلة أصلاً على `NeutronSourceType`. الثابت المُكرَّر
`Am241HalfLifeYears = 432.2` في `NeutronDecayCalculationService.CalculateAm241ActivityAtDate` كان
سيُعطي نتيجة اضمحلال **خاطئة صامتة** لو استُعمل يوماً لمصدر من نوع غير Am-241. سبب التصحيح داخل
هذا الـPR لا في جولة لاحقة: هذا عيب صحة في كود لم يُدمَج بعد، أرخص وأسلم إصلاحه قبل الدمج من إصلاحه
بعده (ترحيل مخطط إضافي على بيانات إنتاجية محتملة، وواجهة منشورة تحمل تسمية مضلِّلة لفترة).
التغييرات: (1) إعادة تسمية حقول المخطط `Am241ActivityValue`→`ActivityValue`،
`Am241ActivityUnitId`→`ActivityUnitId`، خاصية التنقل `Am241ActivityUnit`→`ActivityUnit`، بترحيل
جديد `RenameNeutronSourceActivityToGeneric` (لا تعديل على ترحيل الجولة 126 المدفوع أصلاً لـmain) —
ولّد EF Core فعلياً عمليات `RenameColumn`/`RenameIndex` حقيقية (لا Drop+Add) تلقائياً. (2) إعادة
تسمية دالتي الحساب `CalculateCurrentAm241Activity`→`CalculateCurrentSourceActivity` و
`CalculateAm241ActivityAtDate`→`CalculateSourceActivityAtDate` في `INeutronDecayCalculationService`/
`NeutronDecayCalculationService` (كانتا خارج نطاق عقد الجولة 128 الأصلي — أُدرجتا الآن حصراً لهذا
التصحيح بتفويض مباشر من إدريس). (3) حذف الثابت `Am241HalfLifeYears` نهائياً واستبداله بقراءة
`source.NeutronSourceType.HalfLife`/`HalfLifeUnit` عبر `TryConvertToSeconds` الموجودة أصلاً — يطابق
حرفياً كيف تتعامل `CalculateEmissionRateAtDate` (نفس الملف) مع نصف العمر لمعدل الانبعاث؛ هذا يجعل
حالتي `MissingSourceType`/`InvalidHalfLife`/`UnsupportedHalfLifeUnit` قابلة للتحقق فعلياً لمسار
النشاط أيضاً الآن، لا لمعدل الانبعاث فقط. (4) تعميم تسميات الواجهة: «نشاط الأمريسيوم-241» →
«النشاط الإشعاعي»، «وحدة نشاط الأمريسيوم-241» → «وحدة النشاط»، «النشاط الحالي المحسوب
للأمريسيوم-241» → «النشاط الحالي المحسوب» (`SourcesView.xaml`)، وتعميم رسائل الخطأ العربية
المقابلة في `NeutronSourceService.cs`. (5) تحديث ثلاثة ملفات اختبار: `SourcesViewModelTests.cs`
(الجولة 128)، و`NeutronSourceServiceTests.cs`/`NeutronDecayTests.cs` (الجولتان 126/127، مدفوعتان
أصلاً لـmain) — إعادة تسمية المراجع، وإضافة `NeutronSourceType.HalfLife`/`HalfLifeUnit` صريحة لكل
اختبار يتوقّع `Calculated` (لم تكن مطلوبة سابقاً حين كان الثابت مُكرَّراً)، زائداً اختباري تحقّق
جديدين في `NeutronDecayTests.cs`: `SourceActivity_MissingSourceType_ReturnsUncalculated` (يثبت أن
هذه الحالة صارت قابلة للتحقّق فعلياً)، و`SourceActivity_Pu238_UsesSourceTypeHalfLife_NotAm241HardcodedValue`
— يثبت الاضمحلال الصحيح لمصدر Pu-238/Be (نصف عمر 87.7 سنة، ليس 432.2) بعد نصف عمر واحد بالضبط
يُعطي نصف القيمة، مع تأكيد صريح أن النتيجة **تختلف** عمّا كان سيُنتجه الثابت القديم المُكرَّر لو
بقي — لا تعميم تسمية فقط، بل إثبات أن الحساب الفعلي صحيح لنوع غير الأمريسيوم-241. 1133/1131 اختباراً
(Debug/Release، +2 عن الجولة 128 الأصلية 1131/1129؛ فشل اختبار واحد مؤقتاً بتوكيد نص عربي قديم
`Create_WithUnknownActivityUnitId_ReturnsFailure` أُصلح فوراً)، نفس 5 تحذيرات `CS8604` المسبقة
(بلا علاقة) و0 أخطاء. **انحراف عملية مُسجَّل:** تعذّر تشغيل `round-implementer` داخل نفس worktree
الفرع القائم بسبب عزل الوكيل الفرعي (sandbox) عن مسارات worktree غير مسارِه المخصَّص — قيد بيئي لا
قرار تصميم؛ نُفِّذ التصحيح مباشرة من جلسة القائد بنفس نمط التحقق (بناء، اختبار كامل مرتين، فحص نطاق
الملفات) بدل التفويض، وسُجِّل صراحة كانحراف عن سير العمل المعتاد.

**الجولة 129 (خارج تسلسل ب4 الفرعي — عرض قراءة فقط في نافذة منفصلة):** أغلقت الفجوة التي
استثناها نطاق الجولة 128 صراحة ("`NeutronSourceDetailsViewModel.cs`/`Window.xaml` — نافذة
تفاصيل/شهادات للقراءة فقط، غير ذات صلة"): مصدر نيتروني بنشاط مُدخَل يعرضه نموذج التحرير بشكل صحيح
منذ الجولة 128 لكن نافذة تفاصيله (أيقونة العين في قائمة المصادر) لم تكن تعرض أي معلومة نشاط إطلاقاً،
رغم أن معدل الانبعاث المماثل يعرض صفَّي معايرته وحسابه الحالي هناك. أُضيفت `NeutronSource.
ActivityValueFormatted` (`[NotMapped]`, `Models/AllModels.cs`) و`ActivityValueFormatted`/
`ActivityDecayResult`/`CurrentActivityDisplay`/`IsCurrentActivityCalculated` (`NeutronSourceDetails
ViewModel.cs`) بنفس نمط `CalibratedEmissionRateFormatted`/`CurrentEmissionRateDisplay` القائم في
النافذة نفسها، وصفان جديدان في `NeutronSourceDetailsWindow.xaml`. استهلاك قراءة فقط للحساب والمخطط
القائمين منذ الجولتين 126/127 — بلا مساس بـ`NeutronDecayCalculationService`/`NeutronSourceService`،
وبلا ترحيل مخطط. اختبار انحداري صريح (على مستوى الموديل وعلى مستوى الـViewModel معاً) يثبت أن
`ActivityUnit == null` رغم `ActivityUnitId.HasValue` لا يرمي استثناءً على طبقة العرض — نفس فئة عيب
الجولة 127 لكن هنا مُتحقَّق منها استباقياً قبل الشحن لا مُكتشفة بعده. 1137/1135 اختباراً
(Debug/Release، +4 عن الجولة 128). التفاصيل الكاملة في `session-summary.md`.

**ب4 مكتملة وظيفياً بالكامل (الجولات 123–128)، وعرض النشاط الآن مربوط بواجهة كلا الشاشتين —
نموذج التحرير (128) ونافذة التفاصيل (129) — ومُتحقَّق منه بصرياً بالكامل من إدريس (حالتا "يوجد
نشاط" و"لا نشاط مسجَّل" كلتاهما، بلقطات شاشة حقيقية من `bin\Debug\net8.0-windows`).** الحاجز
التالي هو **ب5 — إكمال الترجمة
الإنجليزية**، الذي يشمل الآن أيضاً الانحرافين المُسجَّلين في الجولتين 125 و128 (رسائل نصية عربية
حرفية بلا مفاتيح ترجمة). شاشة الأنواع المرجعية المستقلة بالكامل على نمط مكتبة النظائر وقسم
المراجع العلمية داخلها ما زالا تحسينَي واجهة مؤجَّلين اختيارياً (الوظيفة الجوهرية لأنواع المصادر
المرجعية موجودة ضمن نافذة قائمة منذ الجولة 125) ولا يُعدّان حاجزي نشر.

### ☐ ب5 — إكمال الترجمة الإنجليزية
56 نصاً عربياً مثبتاً خارج `DynamicResource` + 7 أعمدة `DataGrid`. أكثرها في `BorrowView.xaml` و `SettingsView.xaml` و `SourceDetailsWindow.xaml`. يُنفَّذ بعد ب4 لتشمل شاشاتها.

**الجولة 130 (فرعية أولى لـب5، منجزة محلياً):** غلَّفت كل رسالة تحقق/نجاح في `NeutronSourceService.
cs` (Create/Update/Delete/Restore) بنمط `TranslationHelper.GetString/GetFormat` مع ارتداد عربي
مطابق حرفياً للنص الأصلي — 20 رسالة (منها الانحرافان المُسجَّلان صراحة في الجولتين 125 و128: رسائل
طول/قطر الكبسولة، ورسالة «كلاهما أو لا شيء» لقيمة النشاط)، بلا أي تغيير في منطق التحقق نفسه ولا في
نصوص `_auditService.LogWithChanges` الوصفية (تبقى عربية دوماً بقرار معماري منفصل عن لغة الواجهة).
20 مفتاح ترجمة جديد أُضيف لكلا القاموسين. اختبار انحداري جديد
(`Create_ValidationAndSuccessMessages_UseEnglishStrings_WhenEnglishLanguageActive`) يُبدِّل قاموس
الموارد النشط فعلياً إلى `Strings.en.xaml` (بنفس آلية تبديل القاموس المُستعملة إنتاجياً في
`App.ApplyLanguage`، عبر URI مطلق بدل النسبي لأن الأخير لا يُحل خارج التطبيق المُعبَّأ) ويثبت أن
النص الإنجليزي الفعلي يُعرَض لا الارتداد العربي. 1138/1136 اختباراً (Debug/Release، +1 عن الجولة
129). **الانحرافان الموثَّقان أعلاه (الجولتان 125/128) أُغلقا الآن بهذه الجولة.** النطاق المتبقي
من ب5 مؤجَّل صراحة لجولات لاحقة: خدمات أخرى تطابق نفس النمط (رسائل عربية حرفية غير مُغلَّفة في
خدمات مشابهة لـ`NeutronSourceService`)، `ViewModels` (مثل `SourcesViewModel.cs`/
`NeutronSourceTypesViewModel.cs`)، و56 النص العربي المثبت + 7 أعمدة `DataGrid` في ملفات XAML
(`BorrowView.xaml`، `SettingsView.xaml`، `SourceDetailsWindow.xaml` وغيرها) المذكورة أعلاه — لم
تُمسّ أي منها في هذه الجولة.

**الجولة 131 (فرعية ثانية لـب5، منجزة محلياً):** غلَّفت كل رسالة تحقق/نجاح في `SourceService.cs`
(Create/Update/Delete/Restore) بنفس نمط الجولة 130 — `TranslationHelper.GetString` مع ارتداد عربي
مطابق حرفياً للنص الأصلي، و`string.Format` (لا `GetFormat` مباشرة) للرسائل ذات المتغيرات لأن
`GetFormat` تُرجع اسم المفتاح نفسه عند غيابه وهذا غير آمن كارتداد. 17 مفتاح ترجمة جديد + إعادة
استخدام `MsgErrNeutronSourceCodeExists` الموجود من الجولة 130 لتطابق نص «كود المصدر موجود بالفعل»
حرفياً بلا تكرار مفتاح. بلا أي تغيير في منطق التحقق نفسه ولا في نصوص `_auditService.LogWithChanges`
الوصفية (تبقى عربية دوماً). **قيد معماري موثَّق:** رسالة نجاح `RestoreSource` تحتوي `source.
ArabicStatus` — خاصية `[NotMapped]` تُعيد نصاً عربياً دائماً بصرف النظر عن لغة الواجهة؛ غُلِّف قالب
الرسالة نفسه بالترجمة لكن قيمة الحالة المعروضة داخله تبقى عربية، معالجتها مؤجَّلة لجولة منفصلة
مستقبلاً. اختبار انحداري جديد
(`CreateSource_ValidationAndSuccessMessages_UseEnglishStrings_WhenEnglishLanguageActive`) بنفس آلية
تبديل القاموس المستعملة في الجولة 130. 1139/1137 اختباراً (Debug/Release، +1 عن الجولة 130). النطاق
المتبقي من ب5 (خدمات أخرى، `ViewModels`، ونصوص XAML المثبتة) ما زال مؤجَّلاً لجولات لاحقة.

**الجولة 132 (فرعية ثالثة لـب5، منجزة محلياً):** غلَّفت كل رسالة تحقق/نجاح في `UserService.cs`
(`Login`/`CreateUser`/`UpdateUser`/`ResetPassword`/`UnlockAccount`/`DeleteUser`/`RestoreUser`/
`ToggleUserFreeze`) بنفس نمط الجولتين 130/131 — `TranslationHelper.GetString` مع ارتداد عربي مطابق
حرفياً للنص الأصلي، و`string.Format` (لا `GetFormat` مباشرة) للرسائل ذات المتغيرات. **حالة معمارية
خاصة جديدة**، غير مسبوقة في الجولتين 130/131 (كانتا تتعاملان مع خاصية `[NotMapped]` ثابتة اللغة
عربياً كـ`ArabicStatus`): `ToggleUserFreeze` كانت تبني متغيراً محلياً `string action` ("تنشيط" أو
"تجميد") يُستخدم في رسالتين مختلفتين — تفصيل سجل التدقيق (يجب أن يبقى عربياً دوماً) ورسالة الإرجاع
للمستخدم (يجب أن تُترجَم بالكامل). فُصل المتغير إلى اثنين: `action` (عربي ثابت، دون تغيير، يُستخدم
حصراً في نص `_auditService.LogWithChanges` الوصفي) و`translatedAction` (يُختار عبر
`TranslationHelper.GetString("TextActivate"/"TextDeactivate")` حسب لغة الواجهة النشطة، يُستخدم
حصراً في رسالة الإرجاع `MsgSuccessToggleFreeze`) — فلا يتسرب أثر الترجمة إلى سجل التدقيق ولا يبقى
أي استثناء معماري في رسالة المستخدم. 20 مفتاح رسالة جديد + مفتاحا الكلمة `TextActivate`/
`TextDeactivate` (بلا إعادة استخدام مفاتيح موجودة — لا تصادم أسماء مع أي خدمة سابقة). بلا أي تغيير
في منطق التحقق أو القفل بعد المحاولات الفاشلة (5 محاولات، 15 دقيقة) أو صلاحيات `RequireAdmin` ولا
في نصوص `_auditService.Log`/`LogWithChanges` الوصفية (تبقى عربية دوماً). اختبار انحداري جديد
(`Login_And_ToggleUserFreeze_Messages_UseEnglishStrings_WhenEnglishLanguageActive`) بنفس آلية تبديل
القاموس المستعملة في الجولتين 130/131، يثبت رسالتي فشل/نجاح تسجيل الدخول بالإنجليزية إضافة لحالة
`ToggleUserFreeze` المترجمة (الكلمة الخاصة معاً). 1140/1138 اختباراً (Debug/Release، +1 عن الجولة
131). النطاق المتبقي من ب5 (خدمات أخرى، `ViewModels`، ونصوص XAML المثبتة) ما زال مؤجَّلاً لجولات
لاحقة.

**الجولة 133 (فرعية رابعة وأخيرة لـب5 من ناحية طبقة الخدمات):** غلَّفت كل رسالة تحقق/نجاح في
`BorrowService.cs` (`CreateRequest`/`MarkReturned`) بنفس نمط الجولات 130-132 —
`TranslationHelper.GetString` مع ارتداد عربي مطابق حرفياً **بما فيه علامات الترقيم** (رسائل هذا
الملف تنتهي بنقطة، بخلاف نظائرها اللفظية في `SourceService.cs` — لم تُدمَج المفاتيح رغم تشابه
المعنى لأن النصين ليسا متطابقين حرفياً). مفتاح واحد جديد (`MsgErrActiveBorrowExists`) أُعيد
استخدامه في موضعين متطابقين حرفياً (التحقق العادي عند الإنشاء و`catch (DbUpdateException)`)، ومفتاح
قالب واحد جديد (`MsgErrGenericWithDetail`, "حدث خطأ: {0}") عبر `string.Format` في كِلا معالجي
`catch (Exception ex)` بالملف — `CreateRequest` يمرر تفصيلاً يشمل `InnerException` إن وُجد،
`MarkReturned` يمرر `ex.Message` فقط، دون أي تغيير في منطق بناء نص التفاصيل نفسه. **انحراف موثَّق
عن نص العقد:** كلمة "غير معروف" (ارتداد كود المصدر المفقود في `MarkReturned`) لم تُغلَّف بمفتاح
جديد (`TextUnknown`) كما اقترح العقد — عند القراءة تبيّن أنها تُستخدم حصراً داخل نص
`_auditService.Log` الوصفي، وسجلات التدقيق مستثناة تماماً من الترجمة بقرار معماري ثابت (تبقى عربية
دوماً)؛ لو غُلِّفت لتسرَّب نص إنجليزي إلى سجل تدقيق يجب أن يبقى عربياً بالكامل — نفس فخ الجولة 132
(`action`/`translatedAction`)، لكن هنا لا يوجد استهلاك آخر للكلمة يبرر مفتاحاً منفصلاً، فتُرك النص
العربي المباشر بلا أي مفتاح غير مُستخدَم. بلا أي تغيير في منطق فحص حالة المصدر أو فحص التسرب أو
الاستعارة النشطة أو تواريخ الإرجاع، ولا في نصوص `_auditService.Log` الوصفية في الدوال الأربع
(`CreateRequest`, `MarkReturned`, `CheckAndUpdateOverdue` بحالتيها) التي تبقى عربية دوماً بتصميم
مقصود ومستثناة صراحة. 9 مفاتيح رسالة جديدة. اختبار انحداري جديد
(`CreateRequest_And_MarkReturned_Messages_UseEnglishStrings_WhenEnglishLanguageActive`) بنفس آلية
تبديل القاموس المستعملة في الجولات 130-132، يثبت رسالة فشل واحدة (محاولة استعارة مصدر غير موجود)
ورسالتي نجاح (`CreateRequest` ثم `MarkReturned`) بالإنجليزية الصحيحة. 1141/1139 اختباراً
(Debug/Release، +1 عن الجولة 132). **سلسلة ب5 الفرعية لطبقة الخدمات مكتملة الآن**
(`NeutronSourceService` ← `SourceService` ← `UserService` ← `BorrowService`). النطاق المتبقي من ب5
(`ViewModels` ونصوص XAML المثبتة، بما فيها 56 نصاً + 7 أعمدة `DataGrid` في `BorrowView.xaml`/
`SettingsView.xaml`/`SourceDetailsWindow.xaml` وغيرها) ما زال مؤجَّلاً لجولات لاحقة.

**الجولة 134 (خامسة، تصحيح نطاق بعد إعلان الاكتمال في الجولة 133 — مدموجة، PR #23، commit
`49fad42dd61ebc82a5d9bfb371fecb10f014fdcc`):** عند مراجعة طبقة الخدمات تبيّن
أن `LocationService.cs` لم تُشمَل ضمن السلسلة الأربعية السابقة رغم مطابقتها لنفس النمط، فغُلِّفت كل
رسالة تحقق/نجاح في `Create`/`Update`/`Delete`/`Restore` بنفس نمط الجولات 130-133 —
`TranslationHelper.GetString` مع ارتداد عربي مطابق حرفياً، و`string.Format` (لا `GetFormat` مباشرة)
للرسائل ذات المتغيرات (`MsgErrCannotDeleteLocationHasSources` باسم الموقع،
`MsgErrCannotRestoreLocationNameConflict` باسم الموقع المتعارض، `MsgSuccessLocationRestored` باسم
الموقع المُسترجَع). دمج صريح لأربعة مفاتيح متكررة حرفياً بين الدوال: `MsgErrInvalidLocationData`
("بيانات الموقع غير صالحة"، `Create`+`Update`)، `MsgErrLocationNameRequired` ("اسم الموقع مطلوب"،
`Create`+`Update`)، `MsgErrLocationNameExists` ("اسم الموقع موجود بالفعل"، `Create`+`Update`)،
و`MsgErrLocationNotFound` ("الموقع غير موجود"، `Update`+`Delete`+`Restore`). 11 مفتاح رسالة جديد
إجمالاً. `guard.Message` من `AuthorizationGuard.RequireEditor` في `Delete`/`Restore` بقي دون أي
تعديل، ونصوص `_auditService.LogWithChanges` الوصفية في الدوال الأربع بقيت عربية دوماً بتصميم مقصود
ومستثناة صراحة من الترجمة. بلا أي تغيير في منطق التحقق من التكرار أو فحص الارتباط بمصادر (`Sources`
أو `NeutronSources`) أو شرط الاسترجاع. اختبار انحداري جديد
(`Create_And_Delete_Messages_UseEnglishStrings_WhenEnglishLanguageActive`) بنفس آلية تبديل القاموس
المستعملة في الجولات 130-133، يثبت رسالة فشل واحدة (اسم موقع فارغ) ورسالتي نجاح (`Create` ثم
`Delete`) بالإنجليزية الصحيحة. 1142/1140 اختباراً (Debug/Release، +1 عن الجولة 133). **سلسلة ب5
الفرعية لطبقة الخدمات مكتملة الآن فعلياً** (`NeutronSourceService` ← `SourceService` ←
`UserService` ← `BorrowService` ← `LocationService`)، مع تحفظ أن أي خدمة أخرى لم تُراجَع بعد صراحة
قد تكشف نفس الفجوة. النطاق المتبقي من ب5 (`ViewModels` ونصوص XAML المثبتة) ما زال مؤجَّلاً لجولات
لاحقة.

**الجولة 135 (سادسة ضمن سلسلة ب5 — مدموجة، PR #25، commit الدمج
`8ecbdf08b75023fff64d6a663c741c2965adbe40`):** أول ملف في هذه السلسلة يجمع بين رسائل مُترجَمة سابقاً (خمس
رسائل تحقق من كون القيمة رقماً منتهياً لنصف العمر/الطاقة/المردود/حد الإعفاء/ثابت غاما، بالمفاتيح
`MsgErrInvalidHalfLifeFinite` وأخواتها، من الجولتين 109/110 تقريباً) ورسائل متبقية لم تُغلَّف بعد —
وليس ملفاً كاملاً غير مُترجَم كالملفات الخمسة السابقة (`NeutronSourceService`/`SourceService`/
`UserService`/`BorrowService`/`LocationService`). غُلِّفت كل الرسائل الـ12 المتبقية في
`RadioisotopeService.cs` (`Create`/`Update`/`Delete`/`Restore`) بنفس نمط الجولات 130-134، مع دمج
صريح لأربعة مفاتيح متكررة حرفياً بين الدوال: `MsgErrInvalidRadioisotopeData`
("بيانات النظير غير صالحة"، `Create`+`Update`)، `MsgErrHalfLifeMustBePositive`
("نصف العمر يجب أن يكون أكبر من صفر"، `Create`+`Update`)، `MsgErrRadioisotopeSymbolExists`
("رمز النظير موجود بالفعل"، `Create`+`Update`)، و`MsgErrRadioisotopeNotFound`
("النظير غير موجود"، `Update`+`Delete`+`Restore`). مفتاح جديد منفصل عمداً `MsgErrInvalidEnergy`
("قيمة الطاقة غير صالحة") لعدم دمجه مع `MsgErrInvalidEnergyFinite` الأطول والمُترجَم سابقاً رغم
تشابه الصياغة. `string.Format` (لا `GetFormat` مباشرة) لرسالتي المتغيرات
(`MsgErrCannotRestoreRadioisotopeSymbolConflict` برمز النظير المتعارض،
`MsgSuccessRadioisotopeRestored` باسم النظير المُسترجَع). الخمس رسائل المُترجَمة سابقاً
(`MsgErrInvalidHalfLifeFinite`/`MsgErrInvalidEnergyFinite`/`MsgErrInvalidYieldFinite`/
`MsgErrInvalidExemptionLimitFinite`/`MsgErrInvalidGammaConstantFinite`) بقيت بلا أي لمس. `guard.Message`
من `AuthorizationGuard.RequireEditor` في `Delete`/`Restore` بقي دون تعديل، ونصوص
`_auditService.LogWithChanges` الوصفية بقيت عربية دوماً بتصميم مقصود. بلا أي تغيير في منطق التحقق من
تكرار الرمز أو فحص الارتباط بمصادر أو شرط الاسترجاع. اختبار انحداري جديد
(`Create_Messages_UseEnglishStrings_WhenEnglishLanguageActive`) بنفس آلية تبديل القاموس المستعملة في
الجولات 130-134، يثبت رسالة فشل واحدة (نصف عمر غير موجب) ورسالة نجاح واحدة (`Create`) بالإنجليزية
الصحيحة. 1143/1141 اختباراً (Debug/Release، +1 عن الجولة 134). النطاق المتبقي من ب5 (`ViewModels`
ونصوص XAML المثبتة، وأي خدمة أخرى لم تُراجَع بعد صراحة) ما زال مؤجَّلاً لجولات لاحقة.

**الجولة 136 (سابعة ضمن سلسلة ب5):** غُلِّفت الرسائل المتبقية في `NeutronSourceTypeService.cs`
(`Create`/`Update`/`Delete`/`Restore`) بنفس نمط الجولات 130-135. 13 مفتاح رسالة جديد، منها 6 مفاتيح
مُدمَجة صراحة بين الدوال: `MsgErrInvalidNeutronSourceTypeData` ("بيانات نوع المصدر غير صالحة"،
`Create`+`Update`)، `MsgErrNeutronSourceTypeCodeRequired` و`MsgErrNeutronSourceTypeNameEnRequired`
(`Create`+`Update`)، `MsgErrInvalidPhotonToNeutronDoseRatioFinite` (`Create`+`Update`)،
`MsgErrNeutronSourceTypeCodeExists` ("رمز نوع المصدر موجود بالفعل"،
`Create`+`Update`)، و`MsgErrNeutronSourceTypeNotFound` ("نوع المصدر غير موجود"،
`Update`+`Delete`+`Restore`). مفتاح إضافي (رابع عشر إجمالاً، غير محسوب ضمن الـ13 الجديدة) مُعاد
استخدامه لا مُكرَّراً: تحقق `HalfLife <= 0` في
`Create`/`Update` كان يحمل نفس النص الحرفي الذي أنشأته الجولة 135 لـ`RadioisotopeService.cs`
(`"نصف العمر يجب أن يكون أكبر من صفر"`)، فأُعيد استخدام مفتاحها `MsgErrHalfLifeMustBePositive`
الموجود مسبقاً في المورد دون أي تكرار أو إضافة مفتاح جديد بنفس النص. `string.Format` (لا `GetFormat`
مباشرة) لرسالتي المتغيرات (`MsgErrCannotRestoreNeutronSourceTypeCodeConflict` برمز النوع المتعارض،
`MsgSuccessNeutronSourceTypeRestored` برمز النوع المُسترجَع). الثلاث رسائل المُترجَمة سابقاً من
الجولة 110 (`MsgErrInvalidHalfLifeFinite`/`MsgErrInvalidMeanNeutronEnergyFinite`/
`MsgErrInvalidAmbientDoseConversionFinite`) بقيت بلا أي لمس، وكذلك تحقق `PhotonToNeutronDoseRatio`
غير المنتهي المُضاف في الجولة 123 (نُقل فقط إلى مفتاح جديد `MsgErrInvalidPhotonToNeutronDoseRatioFinite`
دون تغيير في الشرط نفسه). `guard.Message` من `AuthorizationGuard.RequireEditor` في `Delete`/`Restore`
بقي دون تعديل، ونصوص `_auditService.LogWithChanges` الوصفية بقيت عربية دوماً بتصميم مقصود. بلا أي
تغيير في منطق التحقق من تكرار الرمز أو فحص الارتباط بمصادر نيترونية أو شرط الاسترجاع. اختبار انحداري
جديد (`Create_Messages_UseEnglishStrings_WhenEnglishLanguageActive`) بنفس آلية تبديل القاموس
المستعملة في الجولات 130-135، يثبت رسالة فشل واحدة (نصف عمر غير موجب) ورسالة نجاح واحدة (`Create`)
بالإنجليزية الصحيحة. 1144/1142 اختباراً (Debug/Release، +1 عن الجولة 135). النطاق المتبقي من ب5
(`ViewModels` ونصوص XAML المثبتة، وأي خدمة أخرى لم تُراجَع بعد صراحة) ما زال مؤجَّلاً لجولات لاحقة.

**الجولة 137 (أولى ضمن طبقة `ViewModels` من ب5):** غُلِّفت كل النصوص العربية الظاهرة للمستخدم
المتبقية بلا غلاف في `SourcesViewModel.cs`. 10 مفاتيح جديدة: `DecayStatusNotRecorded` (نفس المفتاح
لحالتي `NeutronDecayCalculationStatus.NotRecorded` والقيمة الافتراضية `_` في نفس تعبير `switch`، دون
تكرار)، `DecayStatusMissingActivityUnit`، `DecayStatusInvalidActivityValue`،
`MsgErrNeutronServiceUnavailable` (نفس المفتاح الواحد لثلاثة مواضع متطابقة نصياً عند
`_neutronSourceService` الفارغة في `Delete`/`Create`/`Update`)، `MsgErrActivityValueUnitTogether`،
`TitleInventoryReportPdf`، `TitleInventoryReportExcel`، وزوج جديد بأسماء غير متعارضة عمداً:
`MsgErrCapsuleLengthPositiveNumber`/`MsgErrCapsuleDiameterPositiveNumber` و
`MsgErrActivityValuePositiveNumber`. **انحراف مسجَّل (اكتُشف قبل الكتابة، لا بعدها):** أسماء المفاتيح
الأصلية التي طلبها نص العقد (`MsgErrCapsuleLengthPositive`/`MsgErrCapsuleDiameterPositive`/
`MsgErrActivityValuePositive`) تتعارض فعلياً مع ثلاثة مفاتيح موجودة مسبقاً من الجولة 123 بنفس
الاسم تماماً لكن بنص عربي مختلف قليلاً (بلا كلمة "رقماً")، تخدم `NeutronSourceService.cs` — ملف خارج
نطاق هذه الجولة، فتعذَّر توحيد الصياغة فيه. تفادياً لتلويث نص Enum `NeutronSourceService` القائم أو
انتهاك تعليمة "لا تُغيّر النص، فقط غلِّفه"، استُخدمت ثلاثة أسماء مفاتيح جديدة ومختلفة بلاحقة `Number`
تحافظ على نص `SourcesViewModel.cs` الحرفي الأصلي دون أي تعديل. مفتاحان مُعاد استخدامهما دون تكرار:
`MsgErrCannotEditActiveBorrowSource` (أنشأته الجولة 131 لـ`SourceService.cs`، نفس النص الحرفي في
منع تعديل الموقع/الحالة لمصدر قيد استعارة نشطة) و`TitleSuccess` (موجود مسبقاً، وحَّد الموضع الوحيد في
الملف الذي كان يستعمل النص الحرفي "نجاح العملية" بدلاً منه، في `DialogHelper.ShowInfo` عند تعديل
مصدر عادي ناجح). لا لمس لنصوص `_auditService.Log`/`LogWithChanges`، ولا تغيير في أي شرط تحقق. اختبار
انحداري جديد
(`EditNeutronSource_WithNoStoredAm241Activity_ShowsEnglishNotRecordedDisplay_WhenEnglishLanguageActive`)
بنفس آلية تبديل القاموس المستعملة في الجولات 130-136، يثبت أن `DecayStatusNotRecorded` يظهر
بالإنجليزية الصحيحة ("Not recorded") عبر السلوك الفعلي لـ`EditNeutronSourceCommand` لا عبر استدعاء
`TranslationHelper` مباشرة فقط. 1145/1143 اختباراً (Debug/Release، +1 عن الجولة 136)، 5 تحذيرات بناء
مسبقة بلا علاقة بهذه الجولة (CS8604) و0 أخطاء. النطاق المتبقي من ب5 (بقية طبقة `ViewModels`، نصوص
XAML المثبتة، وأي خدمة أخرى لم تُراجَع بعد صراحة) ما زال مؤجَّلاً لجولات لاحقة.

**الجولة 138 (ثانية ضمن طبقة `ViewModels` من ب5 — مدموجة، PR #31، commit الدمج
`4a756185177ebaf33d1d3ba9657ebcb4c3b23ffc`):** غُلِّفت كل النصوص العربية الظاهرة
للمستخدم المتبقية بلا غلاف في `NeutronSourceTypesViewModel.cs` بنفس نمط الجولة 137. 8 مفاتيح جديدة
(`MsgErrNeutronTypeCodeRequired`، `MsgErrNeutronTypeHalfLifePositive`،
`MsgErrPhotonToNeutronRatioInvalid`، `TitleSuccessShort`، `MsgErrNeutronReferenceTypeNotFound`،
`MsgConfirmDeleteNeutronType`، `TitleDeleteSuccess`، `TitleDeleteFailed`) وإعادة استخدام صريحة
لثلاثة مفاتيح قائمة (`TitleWarning`، `AlertError`، `AlertConfirmation` — الأخير بنفس نمط
`SourcesViewModel.cs` القائم: قيمة القاموس الفعلية "تأكيد" مع ارتداد محلي مختلف "تأكيد الحذف").
**انحرافان مسجَّلان (اكتُشفا بقراءة `Strings.ar.xaml` الفعلية قبل الكتابة، لا بعدها):**
(1) اسم المفتاح `MsgErrNeutronTypeNotFound` الذي افترضه نص العقد الأصلي لرسالة "النوع المرجعي غير
موجود" **مُستخدَم بالفعل** بقيمة عربية مختلفة تماماً ("نوع المصدر النيتروني المحدد غير موجود") تخدم
ملفاً آخر خارج نطاق هذه الجولة — استُخدم اسم بديل `MsgErrNeutronReferenceTypeNotFound` تفادياً
لتلويث مفتاح قائم (نفس مبدأ انحراف الجولة 137 حول تعارض أسماء الكبسولة). (2) نص عمر النصف في هذا
الملف ("يجب إدخال قيمة عمر نصف موجبة وأكبر من صفر") **غير مطابق حرفياً** لقيمة المفتاح القائم
`MsgErrHalfLifeMustBePositive` ("نصف العمر يجب أن يكون أكبر من صفر")، فأُنشئ مفتاح جديد منفصل
`MsgErrNeutronTypeHalfLifePositive` بدل إعادة الاستخدام — بالضبط كما توقّع نص العقد عند عدم
التطابق. لا استدعاء لـ`_auditService.Log`/`LogWithChanges` في هذا الملف أصلاً (تحقَّق منه صراحة).
بلا أي تغيير في منطق التحقق أو شرط العمل. اختبار انحداري جديد
(`Save_Create_WithEmptyCode_ShowsEnglishRequiredMessage_WhenEnglishLanguageActive`) بنفس آلية تبديل
القاموس المستعملة في الجولات 130-137، يثبت أن `MsgErrNeutronTypeCodeRequired`/`TitleWarning` يظهران
بالإنجليزية الصحيحة عبر السلوك الفعلي لـ`SaveCommand`. 1146/1144 اختباراً (Debug/Release، +1 عن
الجولة 137)، نفس 5 تحذيرات بناء مسبقة بلا علاقة (CS8604) و0 أخطاء. النطاق المتبقي من ب5 (بقية طبقة
`ViewModels`، نصوص XAML المثبتة، وأي خدمة أخرى لم تُراجَع بعد صراحة) ما زال مؤجَّلاً لجولات لاحقة.

**الجولة 150 (تصحيح حجم ب5 — لا ترجمة جديدة لواجهة المستخدم، جولة اكتشاف وتصحيح بيانات فقط):**
عقد الجولة افترض 73 نصاً عربياً متبقياً. الاكتشاف الفعلي (تدقيق برمجي كامل لقاموسي
`Strings.ar.xaml`/`Strings.en.xaml` + بحث عن كل نص عربي حرفي في `*.xaml`/`*.xaml.cs`/`*ViewModel.cs`/
`*Service.cs`) كشف رقماً مختلفاً جوهرياً، فتوقّف التنفيذ فوراً وأُرسل تقرير كامل لإدريس قبل أي تعديل،
تطبيقاً صريحاً لبند التوقف في عقد الجولة.
- **تدقيق القاموسين:** 1341 مفتاحاً عربياً/1342 إنجليزياً. لا مفاتيح ناقصة، لا قيم فارغة. مفتاحان
  فقط يحملان عيباً حقيقياً (قيمة عربية/غير مترجَمة نسيت في القاموس الإنجليزي أو العكس)، من أصل 11
  حالة "قيمة متطابقة" بقيتها سليمة عمداً (اختصارات، أرقام إصدار، رموز نظائر).
- **البحث عن نصوص مُضمَّنة في الكود:** 696 نصاً عربياً حرفياً غير مُدوَّل عبر 135 ملفاً (بعد استبعاد
  الأسطر المرتبطة أصلاً بـ`TranslationHelper`/`DynamicResource`/`StaticResource` والتعليقات ونصوص
  `_auditService.Log`/`LogWithChanges`).
- **قرارات استبعاد معمارية معتمدة من إدريس (وليست نقصاً في العمل):**
  1. `Services/ReportingService.cs` (167 نصاً) — التقارير المُصدَّرة (Excel/PDF) تبقى بالعربية حصراً
     دائماً بصرف النظر عن لغة الواجهة، بنفس منطق القرارات المعمارية الثابتة الأخرى في هذا المستند.
     **مستبعدة نهائياً من ب5، لا جولة لاحقة لها.**
  2. `Services/TestDataGeneratorService.cs` (53 نصاً) — بيانات بذر/تجريبية (أسماء مواقع ومسؤولين
     وهمية)، ليست نصوص واجهة، ترجمتها غير منطقية أصلاً. **مستبعدة نهائياً من ب5.**
  3. `LoggerService.Log*` (30 نصاً، موزّعة عبر عدة ملفات) — سجل تشخيصي داخلي غير ظاهر للمستخدم، يُعامَل
     بنفس منطق استثناء `_auditService.Log` القائم. **مستبعدة نهائياً من ب5.**
- **بند مؤجَّل صراحة لجولة `high-risk` منفصلة مستقبلية (وليس ضمن ب5 العادية):**
  `PhraseFactoryResetConfirmation` — اكتُشف أن عبارة تأكيد إعادة الضبط المعروضة في `SettingsView.xaml`
  مرتبطة بثابت مُثبَّت مباشرة `SettingsViewModel.cs:88`
  (`RequiredResetPhrase => "إعادة ضبط المنظومة"`) مستقل تماماً عن قاموس الموارد. ترجمة قيمة المفتاح في
  `Strings.en.xaml` وحدها — دون ربط `RequiredResetPhrase` بنفس المفتاح عبر `TranslationHelper` — كانت
  ستُنتج عطباً وظيفياً حقيقياً (الواجهة الإنجليزية تطلب من المستخدم كتابة عبارة إنجليزية بينما التحقق
  الفعلي ما زال يشترط العبارة العربية حرفياً)، أي انحداراً لا إصلاحاً. بما أن الملف يقع ضمن نطاق
  `reset/startup` عالي الخطورة في جدول توجيه المخاطر، تقرَّر تأجيله بالكامل لجولة `high-risk` مخصَّصة
  تخضع لمراجعة الملف الكامل والتسلسل بدل التوازي. **لم يُمَسّ أي سطر في `SettingsViewModel.cs` أو
  `SettingsView.xaml` في هذه الجولة.**
- **التنفيذ الفعلي في هذه الجولة (البند 4 من قرار إدريس فقط):**
  - `Strings.ar.xaml`: تصحيح `MsgLangEnglish` من نص إنجليزي منسوخ خطأً ("Language set to English")
    إلى ترجمته العربية الصحيحة ("تم تعيين اللغة إلى الإنجليزية"). المفتاح غير مُستهلَك حالياً من أي
    كود (`MsgLangArabic`/`MsgLangEnglish` كلاهما مفتاحان يتيمان — لا استدعاء لهما في أي ملف `.cs`
    أو `.xaml` عبر `TranslationHelper`/`DynamicResource`)، فالتصحيح تنظيف بيانات بلا أثر وظيفي حالياً،
    لا انحداراً.
  - `Strings.ar.xaml`: إضافة المفتاح الناقص `LabelSerialShort` ("الرقم التسلسلي: ") بنفس نمط
    `LabelSerialNumber`/`ColSerialNumber`/`FieldSerialNumber` القائمة. المفتاح غير مُستهلَك حالياً في
    أي `.xaml`/`.cs` (مفتاح يتيم أيضاً)، فالإضافة بلا أثر وظيفي حالياً.
  - `Sources.Tests/TranslationKeysTests.cs`: أُزيل الإدخال المرجعي `KnownDeadKeys["LabelSerialShort"]`
    (استثناء موروث من الجولة 100 لعدم وجود المفتاح في `Strings.ar.xaml`) لأنه أصبح غير ذي موضوع بعد
    إضافة المفتاح أعلاه؛ تركه كان سيُبقي توثيقاً مضلِّلاً بلا أثر على نتيجة الاختبار.
- **الحجم الفعلي المتبقي من ب5 لجولات لاحقة (151 فصاعداً، ملفاً بملف):** بعد الاستبعادات الثلاثة
  أعلاه، **446 نصاً عربياً حرفياً** عبر **52 ملفاً** (لا 15 كما قُدِّر تقريبياً في تقرير الاكتشاف
  الأولي — العدد الدقيق أعلى). أكبر عشرة ملفات: `DeletionsViewModel.cs` (45)، `UsersViewModel.cs`
  (44)، `DashboardViewModel.cs` (28)، `LeakTestsViewModel.cs` (27)، `IsotopeLibraryService.cs` (26)،
  `SettingsViewModel.cs` (26، باستثناء `PhraseFactoryResetConfirmation` المؤجَّلة أعلاه)،
  `SourceDetailsViewModel.cs` (22)، `BorrowViewModel.cs` (18)، `LocationDetailsViewModel.cs` (16)،
  `BackupService.cs`/`NeutronSourceDetailsViewModel.cs` (14 لكل منهما). البقية موزَّعة على 42 ملفاً
  إضافياً بين 1-13 نصاً لكل ملف (تفصيل كامل بحوزة إدريس خارج هذا المستند لتخطيط الجولات).
- **التصحيح الأهم لهذا القسم:** رقم "73" الوارد أعلى هذا القسم (توثيق الجولة 138) وفي مقدمة عقد
  الجولة 150 **غير دقيق** — كان على الأرجح يقصد فقط النصوص المثبتة في XAML وأعمدة DataGrid ("56 نصاً
  + 7 أعمدة" الأصلية)، متجاهلاً أن معظم طبقة `ViewModels` (بدأ العمل بها في الجولتين 137-138 لملفين
  فقط من 15+ ملف ViewModel فعلي) ما زالت غير مُدوَّلة بالكامل. **الرقم الصحيح لبقية ب5 هو 446 نصاً عبر
  52 ملفاً، بعد استبعاد التقارير المُصدَّرة وبيانات البذر والسجل الداخلي بقرار معماري دائم لا مؤقت.**
  1181/1179 اختباراً (Debug/Release، +35/+40 عن الجولة 138 — الفارق يعكس تراكم جولات 139-149 غير
  الموثَّقة في هذا القسم، لا أثر جولة 150 وحدها)، نفس 5 تحذيرات بناء مسبقة بلا علاقة (CS8604) و0
  أخطاء.

**الجولة 151 (الدفعة الأولى من سلسلة ترجمة طبقة `ViewModels` المتبقية):** غطَّت `DeletionsViewModel.cs`
و`UsersViewModel.cs` بالكامل — أكبر ملفين حسب اكتشاف الجولة 150. عدد النصوص العربية الحرفية المُترجَمة
فعلياً **57 نصاً** (18 في `DeletionsViewModel.cs`، 39 في `UsersViewModel.cs`)، وليس ~89 كما قدَّرت
الجولة 150 تقريبياً (45+44) — الفارق يعود لسببين تحقَّق منهما القائد بقراءة الكود الكامل قبل التنفيذ لا
بالاعتماد على عدّ آلي:
1. تقدير الجولة 150 لـ`DeletionsViewModel.cs` عدَّ على الأرجح كل سطر داخل رسائل التفاصيل متعددة الأسطر
   (مثل رسالة تفاصيل النظير المحذوف ذات 10 أسطر) كعنصر منفصل؛ الأسلوب الصحيح للترجمة هو مفتاح واحد لكل
   قالب رسالة كامل (`TranslationHelper.GetFormat` بعدة متغيرات)، فالعدد الفعلي القابل للترجمة أقل.
2. **6 نصوص عربية حرفية في `UsersViewModel.cs` استُبعِدت عمداً من الترجمة** لأنها مقارنات منطق أعمال ضد
   قيمة `Role.RoleName`/`AuditLog.Action` المخزَّنة في قاعدة البيانات (تبقى عربية دوماً بصرف النظر عن
   لغة الواجهة)، لا نصوص عرض: `UsersViewModel.cs:185` (`AdminUsersCount`)، `:196` (شرط `if` في
   `UpdateRoleSummaries`)، `:209` (شرط الـ`ternary`، لا الوصفين الناتجين عنه — هما مُترجَمان)، `:272`
   (فلترة `Action.Contains("تعديل"/"حذف")`)، `:723` (`PackPermissions`)، `:792`
   (`UpdatePermissionsVisibility`). تغليف أيٍّ منها بـ`TranslationHelper.GetString` كان سيُغيّر السلوك
   الفعلي في وضع اللغة الإنجليزية (المقارنة تفشل دوماً لأن القيمة المخزَّنة تبقى عربية) — مخالفة مباشرة
   لقيد "لا تغيير في منطق الأعمال". **القائد أوقف التنفيذ وأرسل تقرير اكتشاف مفصَّل لإدريس (العدد
   الفعلي 61 حينها، قبل تصحيح إضافي لاحق إلى 57) نظراً لكونه دون الحد الأدنى 65 المحدَّد صراحة في عقد
   الجولة؛ وافق إدريس على المتابعة بالعدد الفعلي المُدقَّق.**
- **مؤجَّل صراحة (موثَّق لا منسي):** فاصلا القوائم العربيان `" ، "` (`UsersViewModel.cs:404`) و`"، "`
  (`:436`، `string.Join` في عرض فروقات الصلاحيات) — علامتا ترقيم لا جملتا نص، خارج نطاق هذه الجولة
  (رسائل/تسميات فقط)، يُعاد النظر فيهما إن استدعت الحاجة لاحقاً.
- **التنفيذ:** كل نص استبدل بـ`TranslationHelper.GetString("Key") ?? "النص العربي الأصلي"` (ثابت) أو
  `GetFormat("Key", args...)` (بمتغيرات)، بنفس نمط الجولات 130-138 تماماً. **56 مفتاح ترجمة جديد** (18
  في `DeletionsViewModel.cs` + 38 في `UsersViewModel.cs`؛ بعضها يُعاد استخدامه أكثر من مرة، مثل
  `TextEmptyNotSpecified`) أُضيف لكلا القاموسين — **تصحيح بعد التحقق المستقل**: رسالة الالتزام الأصلية
  والصياغة الأولى لهذا القسم ذكرتا خطأً "38 مفتاحاً" (عدد مفاتيح `UsersViewModel.cs` فقط، دون جمع
  مفاتيح `DeletionsViewModel.cs` الثمانية عشر معها)؛ اكتشف `change-verifier` المستقل هذا الخطأ بالعدّ
  الفعلي في القاموسين (`grep -c 'x:Key='` على الديف) فصُحِّح هنا. لا أثر وظيفي للخطأ — الكود والقاموسان
  كانا متطابقين فعلياً منذ البداية، الخطأ كان في التوثيق السردي فقط. تواريخ العرض
  (`CalibrationDate`, `CreatedAt`) حُوِّلت لنص بـ`.ToString("yyyy/MM/dd")` قبل تمريرها كوسيط لـ`GetFormat`
  حفاظاً على نفس التنسيق بصرف النظر عن لغة النظام. ارتداد `ns.NeutronSourceType?.NameAr` (لا `NameEn`)
  في رسالة تفاصيل المصدر النيتروني المحذوف بقي كما هو (سلوك سابق للجولة، لم يُمَسّ). بلا أي تغيير آخر
  في منطق التحقق أو تدفق الكود أو نصوص `_auditService.Log`/`LogWithChanges` (تبقى عربية دوماً).
- **الاختبارات:** اختباران انحداريان جديدان (`DeletionsViewModel_Messages_UseEnglishStrings_
  WhenEnglishLanguageActive` في `DeletionsAndAdminPromptTests.cs`، `UsersViewModel_KeyMessages_
  UseEnglishStrings_WhenEnglishLanguageActive` في `UsersViewModelTests.cs`) بنفس آلية تبديل القاموس
  المستعملة في الجولات 130-138، يثبتان رسالة فشل ورسالة نجاح/تأكيد فعليتين بالإنجليزية الصحيحة لكل
  ViewModel. تعمَّد كلا الاختبارين اختيار مسارات متزامنة بالكامل (بلا `await` فعلي) تفادياً لمخاطر
  الجمود (deadlock) عند استدعاء دالة `async` من داخل `Dispatcher.Invoke` المتزامن الذي تستعمله
  `WpfStaFixture.RunInSta`. 1183/1181 اختباراً (Debug/Release، +2/+2 عن الجولة 150)، نفس تحذيرات البناء
  المسبقة بلا علاقة (CS8604 في `LoginWindow.xaml.cs`/`ViewInstantiationTests.cs`) و0 أخطاء.
- **الحجم المتبقي من ب5 بعد هذه الجولة:** 446 (الجولة 150) − 57 (مُترجَم فعلياً) − 6 (استبعاد معماري
  دائم، مقارنات منطق أعمال) = **383 نصاً عربياً** عبر 50 ملفاً متبقياً (`DeletionsViewModel.cs`
  و`UsersViewModel.cs` مكتملان بالكامل الآن، الفاصلان المؤجَّلان في `UsersViewModel.cs` محسوبان ضمن
  الـ383). **انحراف عملية مُسجَّل (يكرر نمط الجولة 128):** تعذّر إكمال `round-implementer` داخل نفس
  worktree بسبب عزل الوكيل الفرعي (sandbox) عن مسارات worktree غير مسارِه المخصَّص — نفدت دورات
  الوكيل الأول (48 دورة) في منتصف التنفيذ قبل كتابة الاختبارات، ومحاولة استئنافه عبر استدعاء `Agent`
  جديد فشلت لنفس سبب العزل (كل استدعاء `Agent` يحصل على worktree منعزل خاص به لا يصل لعمل سابق). القائد
  استخرج التغييرات الصحيحة والمُتحقَّق منها من الوكيل الأول (فحص `git diff` سطراً بسطر مقابل الجدول
  المرسَل في العقد قبل القبول) عبر `git apply` من worktree الوكيل إلى worktree الجولة، ثم أكمل الاختبارات
  والتوثيق والبناء مباشرة من جلسة القائد بنفس نمط التحقق المعتاد (بناء واختبار كامل مرتين، فحص نطاق
  الملفات) بدل التفويض الكامل.

**الجولة 152 (إصلاح خلل قديم — خارج سلسلة ب5، اكتُشف أثناء الاختبار البصري الفعلي لـPR #46):**
اكتشف إدريس بصرياً أن حفظ تعديل مستخدم في `UsersView` لا يُظهر أي رسالة نجاح رغم نجاح العملية فعلياً.
تشخيص القائد بأدلة `git diff`/`git log` مباشرة نفى أي علاقة بالجولة 151 (لا `Save()` ولا `ShowMsg` ولا
`UserService.UpdateUser` ولا `UsersView.xaml` ضمن ديف تلك الجولة إطلاقاً) وحدَّد السبب الجذري الحقيقي:
`UsersViewModel.cs` يُعيِّن `Message`/`HasMessage` بشكل صحيح دوماً (عبر `ShowMsg`)، لكن `UsersView.xaml`
لم يربط هاتين الخاصيتين بأي عنصر مرئي إطلاقاً منذ البداية — خلل قديم موجود في `main` قبل أي جولة ترجمة،
وليس تراجعاً.
- **فحص موسَّع (الخطوة 1 من العقد):** بحث عن كل ViewModel يحمل زوج `_message`/`_hasMessage` (6 ملفات:
  `AlertsViewModel`, `LocationsViewModel`, `NeutronSourceTypesViewModel`, `RadioisotopesViewModel`,
  `SourcesViewModel`, `UsersViewModel`) وقورن كل واحد بالـXAML المقابل له:
  - `LocationsView.xaml` **الوحيد** المربوط بشكل صحيح أصلاً (النمط المرجعي: `Border` بـ
    `Visibility="{Binding HasMessage, Converter={StaticResource BoolToVis}}"` يحوي `TextBlock`
    بـ`{Binding Message}` وزر إغلاق مرتبط بـ`CloseMessageCommand`).
  - **`AlertsView.xaml`, `RadioisotopesView.xaml`, `SourcesView.xaml`, `UsersView.xaml`** — أربع
    شاشات إضافية غير `UsersView.xaml` تعاني من نفس الخلل بالضبط (الـViewModel يُعيِّن `Message`/
    `HasMessage` فعلياً عبر `ShowMsg` أو تعيين مباشر، والـXAML لا يربطهما بأي عنصر). أُدرجت الأربعة
    ضمن نطاق هذه الجولة كما نص العقد.
  - `NeutronSourceTypesViewModel.cs` **استُبعِدت عمداً**: تحمل حقل `_hasMessage` فقط بلا حقل `_message`
    مقابل (لا زوج كامل)، ولا يُعيَّن `_hasMessage` في أي مكان بالملف إطلاقاً (حقل ميت حقيقي) — هذه
    الشاشة تستخدم `DialogHelper.ShowInfo`/`ShowWarning` الفعلية للتغذية الراجعة (12 استدعاءً)، فهي لا
    تعاني من نفس الخلل أصلاً، وإصلاحها يتطلب تعديل الـViewModel (حذف حقل ميت أو إضافة `_message`) وهو
    خارج القيد الصارم "لا تغيير في أي ViewModel" لهذه الجولة.
- **الإصلاح (XAML فقط، بلا لمس أي ViewModel):** أُضيف بانر بنفس نمط `LocationsView.xaml` تماماً (نفس
  اسم المحوِّل `BoolToVis`، نفس تركيب `Border`+`PackIcon`+`TextBlock`) في الملفات الأربعة:
  - `UsersView.xaml`: صف جديد (`Row 1`) بين ترويسة الصفحة وتبويبات التنقل؛ تحوَّلت فهارس صفوف التبويبات
    والمحتوى من 1/2 إلى 2/3.
  - `SourcesView.xaml`: استُخدم صف علوي فارغ كان موجوداً أصلاً بلا استخدام (`Grid.Row="0"` في الشبكة
    الخارجية) — بلا أي تغيير في فهارس الصفوف الأخرى.
  - `RadioisotopesView.xaml`: صف جديد (`Row 1`) بين شريط البحث ومحتوى الجدول؛ تحوَّل فهرس صف الجدول من
    1 إلى 2.
  - `AlertsView.xaml`: صف جديد (`Row 1`) بعد الترويسة؛ تحوَّلت فهارس بطاقات الإحصائيات/الفلاتر/الجدول/
    الترقيم من 1-4 إلى 2-5.
  - **انحراف موثَّق عن التطابق الحرفي مع نمط `LocationsView.xaml`:** زر الإغلاق (`CloseMessageCommand`)
    حُذف من البانر في الشاشات الأربع لأن أياً من ViewModels الأربعة لا يملك أمر `CloseMessageCommand`
    (تحقَّق فعلياً بالبحث — لا وجود له)، وإضافته تتطلب تعديل ViewModel وهو ممنوع صراحة في هذه الجولة.
    البانر يبقى ظاهراً حتى تُستبدَل الرسالة برسالة تالية (سلوك مقبول، موثَّق لا مسكوت عنه).
  - أُضيف `x:Name="MessageBanner"`/`x:Name="MessageBannerText"` لعنصري `Border`/`TextBlock` في الشاشات
    الأربع لتمكين الاختبارات الآلية من تحديدهما (`FindName`)، بلا أي أثر وظيفي على العرض.
- **الاختبارات:** ملف جديد `Sources.Tests/MessageBannerBindingTests.cs` بأربعة اختبارات انحدارية (واحد
  لكل شاشة) تستضيف الـView فعلياً داخل `Window` حقيقية (`Show()` + `UpdateLayout()`)، بنفس نمط
  `SourcesViewNeutronOverlayTests.cs` المعتمد في هذا المشروع — لا فحص XAML ساكن. كل اختبار يثبت: (1)
  البانر مخفي (`Visibility.Collapsed`) قبل تعيين أي رسالة (الحالة الافتراضية `HasMessage=false`)، (2)
  يصبح ظاهراً (`Visibility.Visible`) فعلياً بعد `vm.HasMessage = true`، (3) نص `TextBlock` المعروض
  يطابق حرفياً `vm.Message` المُعيَّن — لا فحص وجود الربط فقط. 1187/1185 اختباراً (Debug/Release، +4/+4
  عن الجولة 151)، نفس تحذيرات البناء المسبقة بلا علاقة (CS8604) و0 أخطاء.
- **البند مُغلَق:** خلل بانر الرسائل القديم في الشاشات الخمس (`UsersView` + أربع شاشات مُكتشَفة) مُصلَح
  بالكامل الآن. `NeutronSourceTypesViewModel`/`Window` مستبعدة عمداً (تغذية راجعة صحيحة فعلاً عبر
  `DialogHelper`، لا خلل حقيقي فيها).

### ☐ ب6 — معالج أول تشغيل
يسأل عن مجلد النسخ الاحتياطي ويُفعّل النسخ التلقائي. الافتراضي الحالي `AutoBackupEnabled = false`.

**الجولة 159 (مُنفَّذة، بانتظار التحقق البصري والدمج):** نافذة `FirstRunWizardWindow` +
`FirstRunWizardViewModel` جديدتان بالكامل، تُعرَضان مرة واحدة قبل `SplashWindow` عبر تعديل وحيد في
`App.xaml.cs OnStartup` (فحص `SettingsHelper.FirstRunWizardCompleted` قبل إنشاء السبلاش، مع
`Application.Current.MainWindow = splash` صريحة للحفاظ على السلوك السابق تحت
`ShutdownMode.OnExplicitShutdown`). علامة الإكمال تُخزَّن عبر `SettingsHelper` (نمط `RememberMe`) لا
عبر `ISystemSettingsService.SaveSetting` — الأخيرة تتجاهل الحفظ صامتة قبل التفعيل عبر
`AuthorizationGuard.RequireActivated`، ولو استُخدمت هنا لعاد المعالج للظهور كل تشغيل أثناء التجربة.
في الوضع التجريبي لا يُستدعى `SaveSetting` إطلاقاً (رسالة توضيحية بدل نجاح مضلِّل)؛ تفعيل النسخ
التلقائي بلا مسار محدد يُوقَف بتحذير دون إغلاق النافذة أو تعليم الإكمال؛ "تخطي" يُعلِّم الإكمال فقط
بلا أي حفظ. `LoginWindow`/`LoginView`/`SplashWindow` بلا أي لمس (مُتحقَّق عبر `git diff --stat` من
المنفِّذ والمُدقِّق المستقل كلاهما). 1239/1239 اختباراً محلياً (Debug، 7 جديدة)، 1237/1237 في CI (Release) — الفارق الثابت المعروف
(`TestDataGeneratorTests.cs` محاط بـ`#if DEBUG` ويُستثنى في بناء CI، انظر سجل الجولة 130 أعلاه)، لا
علاقة له بهذه الجولة. 5 تحذيرات بناء سابقة بلا علاقة (`CS8604`). PR رقم 54
(Draft) — CI أخضر بالكامل، CodeRabbit لم يراجع بعد (تُخطَّى المراجعة التلقائية لطلبات Draft). **لم
يصدر بعد حكم دمج:** بانتظار التحقق البصري الحصري لإدريس (ستة سيناريوهات في وصف الـPR) ثم عبارة
"موافق على الدمج" من القائد التقني تليها موافقة إدريس الصريحة، حسب عقد الجولة.

### ☐ ب7 — الدليل والمساعدة
`HelpView` + PDF. يشمل جرعة الإشعاع، مكتبة النظائر، الحاسبة، فحوصات التسرب، المحذوفات، المصادر النيترونية وصلاحياتها، النسخ والاستعادة.

### ☐ ب8 — نظام النشر
يتبع `MASTER_DEPLOYMENT_PLAN_FOR_WINDOWS_DESKTOP_APPLICATION.md` (105 قسماً) على عشر مراحل، **تبدأ بمرحلة تحليل بلا تعديل** بعد استقرار ب1–ب7.
قرارات مبكرة: بناء واحد على .NET 8 لويندوز 10/11، وتوثيق عدم دعم ويندوز 7 و8.1 بوسم `Not Compatible` صراحةً.
أسئلة معلَّقة: ما نظام التشغيل على الأجهزة المستهدفة فعلاً؟ وهل تتوفر شهادة توقيع رقمي أم نوثّق تحذير SmartScreen؟
مطلوبات جديدة من الخطة لم تكن مرصودة: مصدر حقيقة واحد للإصدار، نافذة About، منع تشغيل نسختين، Silent install وExit codes، SHA-256 للمخرجات، `build-release.ps1`.
**الجولة 167 (جزء أول، قيد المراجعة):** سكربتات مصدرية لمثبِّت Windows حقيقي
عبر Inno Setup 6 — `deploy\installer.iss`/`deploy\build-installer.ps1`/
`deploy\assets\generate-wizard-images.ps1` + `docs\deployment-guide.md` +
خصائص تجميعة وصفية جديدة (`AssemblyProduct`/`AssemblyCompany`/... إلخ) في
`AssemblyInfo.cs`. `[Files]` يقتصر على مجلد النشر إلى `{app}` بلا أي مرجع
لمسارات بيانات المستخدم، ولا `[UninstallDelete]` عمداً. لا توقيع كود بعد.
`ISCC.exe` لم يُشغَّل فعلياً في بيئة التنفيذ (تعذّر تقني في أداة الصدفة، لا
غياب الأداة) — البناء الفعلي والتحقق البصري الخماسي (`docs\deployment-guide.md`
§4) يبقيان مهمة إدريس. **يبقى غير مكتمل حتى إتمام تلك الخطوات.**

---

## 3. متبقٍ وليس مانع نشر

**تسجيل الابتلاع الصامت (جرد 106):** `App.xaml.cs` (المظهر) + `LeakTestsViewModel` + `LoginWindow.xaml.cs` · `DashboardViewModel` + `SettingsViewModel` + `SourceDetailsViewModel` ومعها إظهار `CorruptedKeys` · `SourcesViewModel` + `AlertsViewModel` + `ActivityCalculatorViewModel` · إعادة مسح بمعيار «هل بدأ الفعل بنقرة صريحة من المستخدم؟»

**أمنية مؤجَّلة:** `SeedData` يبتلع فشل ترقية هاش المدير — ~~`PasswordHelper.VerifyPassword` يبتلع فشل BCrypt بلا تسجيل~~ أُغلق في الجولة 117 بإضافة `LoggerService.LogError` داخل catch دون تغيير قيمة الإرجاع أو التوقيع (انظر §5)
- ~~القفل بعد المحاولات الفاشلة~~ — **موجود ويعمل، شُطب من القائمة.** تبيّن بالفحص في الجولة 111 أن `UserService.Login` ينفّذه فعلاً: خمس محاولات فاشلة ثم قفل خمس عشرة دقيقة، مع عدّاد يخبر المستخدم بالمتبقي، وتسجيل في السجل، ودالة `UnlockAccount` للمدير. كان مصنَّفاً مؤجَّلاً استناداً إلى توثيق قديم لا إلى الكود.

**دين تقني:** `SettingsHelper` يهجر الملف القديم بعد فشل نسخ عابر (أُدخل في 105-ب) · `LoggerService.cs:9` يبني مسار السجلات يدوياً · 5 تحذيرات `CS8604` مؤكَّدة من CI وموزَّعة على `LoginWindow.xaml.cs` (السطران 104 و199، مكرران لأن الحل يبني `Sources.csproj` ونسخة `wpftmp` مؤقتة) و `ViewInstantiationTests.cs:304`، وتحذيرات `LoginWindow` لا تُعالَج لأن الملف تحت الحظر المطلق — فهي قرار لا دين مفتوح · تبعثر سلاسل الحالة في 45+ موضعاً بلا `enum` · تباين ألوان الحالة · ازدواج وحدات الحاسبة · تكرار `SimpleArabicStatus` · `TestDataGeneratorService` بلا مصادر نيترونية · توحيد UTC · وضوح رسالة تكرار `SourceCode` عند الاسترجاع · مسح double.TryParse: أُغلقت جميع مسارات الحفظ وقُيِّدت بالخدمات والواجهات (الجولتان 109 و110)، والمتبقي تفاعلي خالص في الذاكرة (ActivityCalculatorViewModel)
- ~~`UsersViewModel.UpdateRoleSummaries` تكتب وصف صلاحيات الدور **نصاً ثابتاً** مُرمَّزاً في الكود~~ — أُغلق في الجولة 116 باستبداله ببيان الضبط الفردي (انظر §5).
- رسالتان عربيتان متقاربتان لمعنى واحد: `MsgErrAdminOnly` القائم («غير مصرح: هذه العملية مخصصة لمدير النظام فقط») يستعمله `PasswordPromptDialog`، و `MsgErrOperationAdminOnly` المستحدث في الجولة 111 للحارس. الفصل كان صحيحاً وقتها تفادياً لتغيير نص قائم، ويلزم توحيدهما لاحقاً.
- اسم الدور «مدير النظام» مكتوب حرفياً في مواضع متفرقة من الكود والاختبارات (`User.IsAdmin`، `UsersViewModel`، `PackPermissions`، وملفات اختبار عدة). نفس نمط تبعثر سلاسل الحالة في البند 3، ويُعالَج معه.
- **كنس الوسيط الشامل (مؤجَّل موثَّق):** جاني فشل CI `#100` كان اختبار `BorrowViewModel_ReceivesSourcesUpdatedMessage` عبر `WeakReferenceMessenger.Default` المشترك الذي أبلغ مستقبِلاً يحمل fixture ميتاً (`no such table: Sources`)، وعزلته الجولة 115 بحقن `IMessenger` في `BorrowViewModel`. كنس الوسيط الكامل في بقية الشاشات (سبعة ViewModels + `IDisposable` في الخمسة الناقصة + عزل وسائط الاختبارات القائمة) مؤجَّل موثَّق لما بعد النشر. **[إصلاح `IsTestMode` أُنجز في 115-ب — انظر §5.]**
- كود الإنتاج يقرأ `DialogHelper.IsTestMode` في أربعة مواضع (`SourceNavigationHelper` ×2، `LocationsViewModel`، `PasswordPromptDialog`) كحارس تخطٍّ — وعيُ اختبارٍ مبثوثٌ في الإنتاج. لا يُعالَج الآن (نطاق 115-ب محصور بتثبيت العلم)؛ يُنظر لاحقًا في عزله خلف واجهة اختبار.
- **حقول نصية رقمية بـ`UpdateSourceTrigger=LostFocus` قد تُحفَظ بقيمة قديمة/فارغة عند الحفظ بمفتاح Enter (اكتشاف CodeRabbit على PR #16 للجولة 128، مؤجَّل موثَّق):** **لم يُغلق نهائياً بعد — التوقف عن الدمج قائم بطلب إدريس الصريح.** اكتشاف بصري فعلي (تشغيل حقيقي) على PR #44 أبلغ عن عطلين ظاهريين بعد الإصلاح الأول لهذه الجولة: (1) `RadioisotopeFormWindow` يُغلق برسالة نجاح كاذبة دون حفظ فعلي للقيمة الجديدة، (2) `SourceFormWindow` لا يستجيب لـEnter إطلاقاً. أُعيدت كتابة اختبارات `RadioisotopeFormWindowTests.cs` لتتحقق من القيمة الفعلية الواصلة لطبقة الخدمة (Moq) لا من خاصية الـViewModel فقط — والنتيجة أن العطل **لم يتكرر آلياً حتى بدون أي إصلاح إضافي**، ما يرجّح اختباراً بصرياً على بناء لم يتضمن كوميت الجولة 149 فعلياً، بانتظار تأكيد إدريس. عطل `SourceFormWindow` الثاني ليس تراجعاً، بل سلوك مقصود موروث من الجولة 148 (الحقول السبعة على الخطوة 2 حيث زر الحفظ `IsDefault` غير نشط). أُضيف تحصين وقائي (`PreviewKeyDown` يُفرِّغ الربط المركَّز قبل Enter) في كلا الملفين `.xaml.cs` بصرف النظر عن نتيجة التشخيص. التفاصيل الكاملة في `docs/session-summary.md` (متابعة الجولة 149). كان النص الأصلي هنا يصف `SourcesView.xaml` القديم (قبل تحويله في الجولة 148 إلى `SourceFormWindow.xaml`) بسبعة حقول: `EditInitialActivityText`، `EditEmissionRateText`، `EditRelativeUncertaintyText`، `EditAnisotropyFactorText`، `EditCapsuleLengthText`، `EditCapsuleDiameterText`، `EditActivityText`. الاكتشاف الفعلي في الجولة 149 (قبل أي تعديل) وسّع الصورة: العدد الحقيقي **11 حقلاً على نافذتين بآليتي سباق مختلفتين**، وليس 7 كما كان مفترضاً — وهذا استوجب توقفاً إلزامياً لتأكيد الاكتشاف مع إدريس قبل المتابعة (بنفس أسلوب التحقق الذي فرضته الجولة 148):
  - **`SourceFormWindow.xaml` (7 حقول، الآلية القديمة نفسها):** بعد تحويل الجولة 148 لم يعد يوجد `KeyBinding` على مستوى النافذة إطلاقاً (قرار متعمد موثّق في تعليق أعلى الملف لتفادي حفظ الخطوة الأولى من المعالج متعدد الخطوات قبل اكتمالها)؛ السباق انتقل إلى زر الحفظ `IsDefault="True"` الظاهر فقط في الخطوة الأخيرة (السطر ~1088).
  - **`RadioisotopeFormWindow.xaml` (4 حقول إضافية اكتُشفت في هذه الجولة، لم تكن موثَّقة سابقاً): `EditHalfLifeText`، `EditEnergyText`، `EditYieldText`، `EditGammaConstantText`.** هذه النافذة تحمل `KeyBinding Key="Return" Command="{Binding SaveCommand}"` صريحاً على `Window.InputBindings` (قرار مثبَّت من الجولة 146: أمر حفظ واحد بلا تفريع يُستدعى من أي خطوة) — أي أن آليتها مطابقة تماماً لافتراض العقد الأصلي.

  **الإصلاح المُطبَّق:** تغيير `UpdateSourceTrigger` من `LostFocus` إلى `PropertyChanged` على الحقول الإحدى عشر جميعها في كلا الملفين، بلا أي تعديل في `SourcesViewModel.cs`/`RadioisotopesViewModel.cs` (معالِجات `On<Property>TextChanged` الجزئية كانت متوافقة مسبقاً — تتعامل مع النص الفارغ/غير القابل للتحليل بإرجاع `null`/`0` دون رمي استثناء أو إعادة كتابة الخاصية النصية). 11 اختبار انحدار جديد (`RadioisotopeFormWindowTests.cs` ×4، `SourceFormWindowTests.cs` ×7) يحاكي KeyDown بمفتاح Enter فعلياً على العنصر المركَّز دون فقدان تركيز يدوي، وأثبت فشله على السلوك القديم عبر `git stash push -u`/`git stash apply` قبل تطبيق الإصلاح. انظر `docs/rounds/149-enter-lostfocus-numeric-fields.md` للعقد الكامل.

**عمود «النشاط الحالي» غائب عن جدول المصادر النيترونية:** جدول المصادر النيترونية (`SourcesView.xaml`)
لا يعرض عمود «النشاط الحالي» رغم أن الحساب (`_neutronDecayService.CalculateCurrentSourceActivity`
عبر `DisplaySourceCurrentActivity`) موجود وصحيح ومُستخدَم فعلاً في نافذة التعديل. جدولا المصادر
العادية والمحذوفة يعرضان عمود `CurrentActivityWithUnit`، بينما جدول المصادر النيترونية لا يحتويه.
مؤجَّل لجولة XAML مستقلة قصيرة بعد إغلاق ب5، لإضافة عمود مماثل بنفس أسلوب التنسيق.

**تعليق متقطّع في عدّاء GitHub:** وقع مرتين في أقل من ساعة على كود مرّ أخضر قبله وبعده، وحُصر أثره بمهلة الوظيفة والخطوة في الجولة 114. المؤشّر: خطوة اختبارات تتجاوز خمس دقائق مع أنها تنتهي في 73 ثانية. الإجراء عند تكراره: إعادة تشغيل التشغيل نفسه بلا تعديل كود، ولا يُفتح تحقيق في الحزمة إلا إن فشل التشغيل المعاد على الـ commit نفسه.

**متابعة:** 51 تعليقاً تراكمياً في CodeRabbit عبر 47 مراجعة · `Build and Test #82` أحمر ولم يُفحص

---

## 4. مرفوض نهائياً — ممنوع إحياؤه

**حساب `H*(10)` للمركّبة المباشرة في المجال الحر.** الشهادة الحقيقية غير متاحة ولن تكون، والحساب بلا معايرة غير قابل للدفاع عنه: معامل التحويل متاح لنوع واحد من عشرة (`Cf-252` بـ 385)، و`AnisotropyFactor` غير مقاس لأي مصدر، وتشتت الغرفة يضيف حتى 40% خارج الحساب. وحيث يظهر مكانه في الواجهة يُعرض نص صريح يوضح أنه غير محسوب وسببه — لا صفر ولا فراغ.

**مؤجَّل بقرار:** توحيد `SourceCertificate.AttachedBy` إلى `Guid?`

---

## 5. مغلق ومُتحقَّق منه

عمود «معدل الجرعة (1م)» حساب جاما مشروع (Γ × النشاط بالـ MBq عند متر) يعالج غير الجاما وناقص الثابت بنصوص صريحة بلا أصفار كاذبة، والمصادر النيترونية معزولة عنه باختبار · توحيد المخطط على EF Migrations (90) · صلاحيات الشاشات على مستوى الواجهة (83) · القائمة المنسدلة لاسم المستخدم (84) · تدفق PR و CodeRabbit (85) · الأس العلمي لمعدل الانبعاث (89) · واجهات المصادر النيترونية (86-ب) · عزل مسارات القاعدة والاستيراد الذرّي (105، 105-ب) · إظهار الفشل التفاعلي وتوحيد الحافظة (107، 107-ب) · عاصفة النسخ الاحتياطي والإعدادات التالفة (108، 108-ب) · التقارير الرقابية · منع القيم غير المنتهية من الدخول إلى القاعدة في الشاشات وطبقة الخدمة (109، 110) · إنفاذ صلاحيات الأدوار في عمليات الحذف والاسترجاع والإدارة الحساسة (17 موضعاً) وسدّ ترقية الامتياز (111) · القفل بعد المحاولات الفاشلة (قائم منذ ما قبل الجلسة) · رفض التاريخ المستقبلي في النسخ الاحتياطي التلقائي وسد ثغرة انقطاع النسخ الدوري وتشديد جودة اختبارات الحافظة والسجل والنسخ (113، 113-ب) · عزل وسيط الرسائل IMessenger في BorrowViewModel واختباره لإنهاء تداخل الحزم المتوازية (115) · تثبيت `DialogHelper.IsTestMode` عبر `[ModuleInitializer]` في تجميعة الاختبار وإزالة كل إسناد متبعثر، فلا يُطفأ ولا يُسرَّب بين الفئات المتسلسلة (115-ب) · تصحيح `UsersViewModel.UpdateRoleSummaries`: إزالة الادّعاء الثابت بقائمة صلاحيات موحّدة لدور «مستخدم» (النظام يضبط الصلاحيات فرديًّا عبر `User.Permissions` لا عبر `Role.Permissions`) — استبدال بجملة صادقة (116) · تسجيل استثناء BCrypt في `PasswordHelper.VerifyPassword` بدل الابتلاع الصامت، عبر `LoggerService.LogError` داخل `catch (Exception ex)` المُسمّى حديثاً، دون تغيير قيمة الإرجاع (تبقى `false`) أو التوقيع أو `HashPassword` (117) · إضافة تسجيل تدقيق لثلاث دوال إدارية في `UserService` كانت لا تكتب أي سجل: `ResetPassword` (سجل بسيط بلا `LogWithChanges` ودون أي أثر لهاش كلمة المرور القديمة أو الجديدة)، و`UnlockAccount` و`ToggleUserFreeze` (سجل بفرق قبل/بعد عبر `LogWithChanges`)، عبر نمط `_auditService ?? new AuditService(...)` المطابق لـ `DeleteUser`/`RestoreUser` كي لا يُسقَط السجل صمتاً عند حقن تبعية تدقيق فارغة (118) · إضافة `OldValues`/`NewValues` إلى `SourceService.CreateSource` و`UpdateSource` عبر `LogWithChanges` بنفس نمط `Delete`/`Restore` القائم في الملف (13 حقلاً خاماً بما فيها `ImagePath`)، أول جولة من ثلاث لإغلاق نفس الفجوة في `LocationService` و`RadioisotopeService` و`NeutronSourceService`/`NeutronSourceTypeService` (119) · إضافة `OldValues`/`NewValues` إلى عمليات `LocationService` الأربع (`Create`/`Update`/`Delete`/`Restore`) عبر `LogWithChanges`، ببناء نمط الفروقات من الصفر (5 حقول خامة: `LocationName`, `LocationType`, `Building`, `Room`, `ResponsiblePerson`، دون `AddedBy`)، مع اختبار جديد لعملية `Restore` التي لم يكن لها اختبار سابقاً في الملف (120) · إضافة `OldValues`/`NewValues` إلى عمليات `RadioisotopeService` الأربع (`Create`/`Update`/`Delete`/`Restore`) عبر `LogWithChanges`، ببناء نمط الفروقات من الصفر (13 حقلاً خاماً: `Name`, `ArabicName`, `Symbol`, `RadiationType`, `HalfLife`, `HalfLifeUnit`, `Energy`, `Yield`, `Category`, `ExemptionLimit`, `GammaConstant`, `Notes`, `EnglishNotes`، دون `AddedBy`)، مع الانتباه لالتقاط `Update` من `existing` قبل إسناد `ArabicName` التلقائي وبعد `SaveChanges()` لتعكس القيمة المُملوءة تلقائياً، ولالتقاط `Delete` بعد حارس المصادر المرتبطة وقبل `IsDeleted = true`؛ أربعة اختبارات جديدة لعملية `Restore` التي لم يكن لها أي اختبار سابقاً في الملف (121) · إضافة `OldValues`/`NewValues` إلى عمليات `NeutronSourceService` و`NeutronSourceTypeService` الأربع (`Create`/`Update`/`Delete`/`Restore`) لكل منهما عبر `LogWithChanges` (12 حقلاً خاماً لكل خدمة)، مع التقاط `Delete` في `NeutronSourceTypeService` بعد حارس المصادر النيترونية المرتبطة وقبل `IsDeleted = true`، وتنسيق التواريخ الاختيارية (`CalibrationDate`, `EmissionCalibrationDate`) بصيغة `yyyy-MM-dd`؛ توسيع اختباري `Restore` القائمين في كلا الملفين بدل تكرارهما — هذه الجولة الرابعة والأخيرة تُغلق دين `OldValues`/`NewValues` بالكامل عبر الخدمات الخمس (122)

---

## فرز تعليقات CodeRabbit — قبل الإصدار (الجولة 112)

الأساس: آخر commit مدفوع d27d0f3، CI #95 أخضر عند 1055 اختباراً.
فُرزت عشرة تعليقات مفتوحة. بعد الإصلاحات: 1063 اختباراً أخضر محلياً (Debug)، و1061 على CI (Release). الفرق ثابت وموثّق: اختبارا TestDataGeneratorTests محاطان بـ #if DEBUG فيستثنيهما بناء Release. CI #96 أخضر على commit 0c85d5f.

### منتهية (10)

| # | الملف | المسألة | القرار |
|---|-------|---------|--------|
| 1 | Helpers/NumericInputParser.cs | تفسير الفاصلة كفاصل آلاف عبر الثقافات يحوّل "1,5" إلى "15" — خطأ بعامل 10 في إدخال علمي | تحليل بثقافة ثابتة مع تمييز صريح بين الفاصل العشري وفاصل الآلاف |
| 3 | Services/SourceService.cs | تسرّب قيم NaN/Infinity عبر كائن التنقل SourceIsotopes قبل الحفظ | رفض أي قيمة غير منتهية في CreateSource وUpdateSource قبل الحفظ |
| 4 | Services/SourceService.cs | استرجاع مصدر محذوف قد ينشئ SourceCode مكرراً مع مصدر نشط — خلل جرد في سجلّ رقابي | فحص وجود رمز نشط مطابق قبل الاسترجاع ومنعه |
| 5 | Helpers/SettingsHelper.cs | فقدان إعدادات المستخدم نهائياً إذا مُسح علم الترحيل رغم فشل النسخ | إبقاء علم الترحيل المعلّق حتى نجاح النسخ المؤكد |
| 2 | Resources/Strings.ar.xaml + Services/NeutronSourceTypeService.cs | مصطلح "معامل تحويل التدفق" ناقص؛ الكمية هي الجرعة المحيطية H*(10) | تصحيح إلى "معامل تحويل التدفق إلى الجرعة المحيطية" في المورد والنصّين الاحتياطيين |
| 11 | Services/SourceService.cs + NeutronSourceService.cs | حماية التاريخ المستقبلي في الـ ViewModel وحده؛ طبقة الخدمة عارية — يخالف مبدأ الجولة 111 | حاجز ثانٍ في طبقة الخدمة يرفض تاريخ معايرة مستقبلياً، مع 6 اختبارات استدعاء مباشر |
| 10 | اختبار توكيد التاريخ المتساهل | التوكيد يقبل تواريخ مستقبلية | أُقفل ضمن #11: اختبارات الخدمة المباشرة تغطي الثغرة |
| 6 | Sources.Tests/ClipboardFeedbackTests.cs | مقارنة نصوص عربية حرفية — هشاشة اختبار | استبدال النصوص المباشرة بثوابت ClipboardCopyHelper وتجريد فحص النجاح من صياغة الواجهة (الجولة 113) |
| 8 | Services/SystemSettingsService.cs | خطأ بمقدار واحد في طول التحذير (51 بدل 50) | استخراج الاقتطاع إلى LogTextHelper.Truncate لضمان حد أقصى 50 محرفاً بدقة وحماية الأزواج البديلة (الجولة 113) |
| 9 | Sources.Tests/BackupScanAndSettingsIntegrityTests.cs | توكيد يفحص عدم الاستثناء لا حقيقة الاقتطاع | إعادة تسمية الاختبار ليعكس فحص عدم رمي استثناء، ونقل اختبار الاقتطاع إلى LogTextHelperTests بـ 10 حالات (الجولة 113) |

فحص جانبي (#10): المسار الطبيعي للواجهة محمي أصلاً — MovedAt يُفرض بـ DateTime.Now
ولا حقل إدخال له؛ تواريخ المعايرة والإرجاع مرفوضة في الـ ViewModel. الثغرة كانت في
الاستدعاء البرمجي المباشر للخدمة، وهي ما سدّه #11.

### مرفوض (1)

| # | الملف | المسألة | سبب الرفض |
|---|-------|---------|-----------|
| 7 | Services/BackupFolderScanner.cs | اقتراح fail-closed عند تعذّر قراءة مجلد نسخ | قرار سلوكي محسوم في الجولة 108-ب: fail-closed يولّد إنذارات كاذبة على أعطال صلاحيات عابرة فيتآكل الثقة في مؤشر النسخ. لا معطى تقني جديد يعيد فتحه |

### مؤجَّل بعد الإصدار (0) — لا يمسّ سلوك المنتَج

لا توجد بنود مؤجلة حالياً — نُقلت البنود 6 و8 و9 إلى المنتهية في الجولة 113.

### دَين تحسين مسجَّل
توحيد المصطلح العربي للكمية H*(10) عبر الكود: رسالة الخطأ تقول "الجرعة المحيطية"،
وتعليق AllModels.cs:965 يقول "المكافئ المحيطي". نفس الكمية، صياغتان. تجميلي، بعد الإصدار.

---

## 6. أفكار بعيدة — لا عمل مطلوب

تحويل المنظومة إلى ويب/هاتف · مهارة عامة من منهجية المراجعة · مهارة لتدفق CodeRabbit/PR

---

## 7. قواعد العمل المثبتة

- جولة واحدة ← دفع ← **قراءة رقم الاختبارات من CI لا لونه** ← الجولة التالية. لا تتراكم الجولات.
- تحديث `session-summary.md` و `release-readiness.md` يُدرَج في برومبت الجولة نفسها ويدخل commitها، لا في commit توثيقي منفصل. الاستثناء: ما لا يُعرف إلا بعد الدفع (رقم CI والتحذيرات).
- عدد التحذيرات يُقرأ من خطوة `Build Solution` في CI لا من البناء المحلي.
- اقتراحات أدوات المراجعة الآلية مُدخلات تشخيصية لا أوامر. ممنوع «Fix all issues» وممنوع تمرير ملف تعليماتها إلى وكيل.
- كل تعليق مرفوض يُغلق في اللوحة ويُدوَّن سببه، وإلا تكرر.

---

## 8. مبادرة معمارية جديدة — التراكب المنبثق داخل العرض (In-View Modal Overlay)

**الجولة 139 (أولى، Draft PR غير مدموج بعد):** بدأت مبادرة معمارية منفصلة تماماً عن سلسلة ترجمة
النصوص (ب5) أعلاه — تحويل نوافذ WPF المستقلة (`Window` + `ShowDialog()`) إلى تراكبات `UserControl`
مدمجة داخل الشاشة الأم مباشرة (`IsVisible`/`Visibility` مربوطة بخاصية `bool` في نموذج العرض الأصل،
بدل نافذة منفصلة بدورة حياة `Owner`/`ShowDialog` خاصة بها). `NeutronSourceTypesWindow` هي أول نافذة
تُحوَّل لهذا النمط الجديد (`NeutronSourceTypesOverlay.xaml` مدمجة داخل `SourcesView.xaml` عبر
`IsManagingNeutronTypes`/`NeutronTypesManagementViewModel` في `SourcesViewModel.cs`) — اختيرت أولاً
لأنها حوار CRUD معزول (إدارة أنواع المصادر النيترونية المرجعية) بلا أي تبعيات تنقّل معقدة أو تعشيش
حوارات فرعية، فتصلح كنموذج أول آمن لإرساء النمط قبل تطبيقه على النوافذ الأربع المتبقية الأكثر
تعقيداً. البنية القابلة لإعادة الاستخدام التي أرستها هذه الجولة: (1) `UserControl` بخلفية معتّمة
(`#88000000`) وبطاقة مركزية بظل، (2) زر إغلاق `✕` صريح في الترويسة يستدعي `CloseCommand` جديداً في
نموذج العرض الأصلي للنافذة (`Action? OnClose` + `[RelayCommand] Close()`)، (3) خاصيتا `bool
IsManaging...`/`...ManagementViewModel?` في نموذج العرض المضيف تتحكمان في الظهور عبر
`BoolToVis` القائم بدل `ShowDialog()`، (4) تمرير `Grid.RowSpan`/`Panel.ZIndex="1000"` ليغطي
التراكب كامل مساحة الشاشة المضيفة. لا تغيير في منطق التحقق أو الحفظ داخل
`NeutronSourceTypesViewModel.cs` سوى إضافة `OnClose`/`CloseCommand`. صُحِّح أيضاً نص XAML واحد تسرّب
من ترجمة الجولة 138 (تلميح حقل `PhotonToNeutronRatio` كان لا يزال عربياً مباشراً) عبر مفتاح جديد
`HintPhotonToNeutronRatio`. حُذفت `NeutronSourceTypesWindow.xaml`/`.xaml.cs` بعد تأكيد خلوّ المستودع
من أي مرجع كودي متبقٍ لهما. **انحراف موثَّق (مصرَّح به مسبقاً من القائد):** تعديل سطر واحد في
`App.xaml.cs` لحذف تسجيل `AddTransient<Sources.Views.NeutronSourceTypesWindow>()` — لم يكن ضمن
قائمة الملفات الأصلية في عقد الجولة لكنه ضروري لعدم كسر البناء بعد حذف النافذة، بلا أي لمسة أخرى
لذلك الملف. 1148 اختباراً محلياً (Debug)، صفر فشل، صفر تجاوز، بلا تحذيرات بناء جديدة (التحذيرات
الخمسة الظاهرة CS8604 مسبقة وغير متعلقة بهذه الجولة: `LoginWindow.xaml.cs`،
`ViewInstantiationTests.cs`). **التحقق البصري الحقيقي من `bin\Debug\net8.0-windows` لم يتم بعد —
بانتظار إدريس قبل اعتبار هذا التراكب مكتملاً بصرياً.**
- التحقق البصري من الإقلاع يلتقطه المستخدم بنفسه لا الوكيل.

---

## 9. الجولة 140 — إصلاح عاجل (Hotfix): عودة تراكب أنواع المصادر النيترونية منعت استخدام قسم المصادر بالكامل

**الحالة: Draft PR غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس معاً — لا دمج قبل الاثنين.**

**العَرَض:** منذ دمج الجولة 139، ظهر تراكب `NeutronSourceTypesOverlay` دائماً فوق قسم المصادر بالكامل
بمجرد فتحه، بلا أي استجابة لزر الإغلاق `✕`، مما منع استخدام القسم كلياً.

**السبب الجذري (دقيق):** العنصر `<views:NeutronSourceTypesOverlay>` في `SourcesView.xaml` كان يحمل
معاً `DataContext="{Binding NeutronTypesManagementViewModel}"` و
`Visibility="{Binding IsManagingNeutronTypes, Converter={StaticResource BoolToVis}}"` على نفس
العنصر. في WPF، إعادة تعيين `DataContext` على عنصر تجعل كل ربط آخر على نفس العنصر (بما فيه
`Visibility`) يُقيَّم بالنسبة للـ`DataContext` الجديد لا الموروث من الأب. بما أن
`NeutronSourceTypesViewModel` (نموذج عرض التراكب نفسه) لا يملك خاصية `IsManagingNeutronTypes`، فشل
ربط `Visibility` بصمت وارتدّت القيمة لافتراضيها الأصلي `Visible` دائماً، بصرف النظر عن حالة
`SourcesViewModel.IsManagingNeutronTypes` الفعلية.

**الإصلاح:** لُفّ `NeutronSourceTypesOverlay` بـ`Grid` خارجي يحمل وحده `Visibility` (فيبقى ضمن سياق
`DataContext` الموروث من `SourcesViewModel`)، بينما `DataContext` الخاص بنموذج التراكب بقي فقط على
العنصر الداخلي `NeutronSourceTypesOverlay` نفسه. لا تغيير آخر في الملف، ولا لمسة لـ
`NeutronSourceTypesOverlay.xaml` أو أي نموذج عرض.

**الدرس المستفاد (الأهم في هذه الجولة):** جميع اختبارات الجولة 139 كانت تتحقق من نجاح الربط منطقياً
(استدعاء أمر يضبط خاصية) لا من القيمة *المحسوبة فعلياً* لـ`Visibility` في الشجرة المرئية الحقيقية —
فمرّت رغم الخلل الحرج. اختبار انحداري حقيقي يتحقق من القيمة النهائية لـ`Visibility` كان سيكتشف هذا
قبل الدمج. كما تبيّن أثناء كتابة هذا الاختبار أن الربط المعتمِد على `DataContext` موروث لا يُفعَّل
(`BindingExpression.Status` يبقى `Unattached`) إلا حين يكون العنصر متصلاً بـ`PresentationSource`
حقيقي (نافذة مضيفة) — فاستدعاء `Measure`/`Arrange`/`UpdateLayout` وحده على عنصر معزول لا يكفي
لتفعيل هذا النوع من الربط، ويُرجع القيمة الافتراضية للخاصية (`Visible`) بصرف النظر عن صحة XAML.
لذلك يستضيف الاختبار الجديد `SourcesView` داخل `Window` حقيقية (خارج الشاشة، `ShowInTaskbar=false`)
قبل قراءة `Visibility` الفعلية — تحقُّق تم التأكد منه يدوياً بإعادة الكود لحالته قبل الإصلاح ومشاهدة
فشل الاختبار (`Visible` بدل `Collapsed` المتوقع) ثم إعادة الإصلاح ومشاهدة نجاحه.

**النتائج:** 1149 اختباراً محلياً (Debug)، صفر فشل، صفر تجاوز (+1 عن الجولة 139: اختبار انحداري
جديد `SourcesView_ByDefault_NeutronTypesOverlayHostIsCollapsed`). بناء المشروع الرئيسي بلا تحذيرات
جديدة (التحذيران الوحيدان CS8604 مسبقان في `LoginWindow.xaml.cs`، ممنوع لمسه بقاعدة المشروع).

**انحراف مسجَّل:** العقد طلب "ملف اختبار جديد"؛ الاختبار أُضيف كطريقة جديدة داخل الملف القائم
`Sources.Tests/ViewInstantiationTests.cs` (بدل ملف منفصل) لأنه يتبع حرفياً نمط الاختبارات القائمة
فيه (`RunInSta`, `FindVisualChildren`) وإنشاء ملف موازٍ كان سيكرر تلك الأدوات المساعدة دون فائدة.

---

## 10. الجولة 141 — In-View Modal Overlay لشاشة المواقع (LocationsView)

**الحالة: Draft PR غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس معاً — لا دمج قبل الاثنين.**

ثاني شاشة تُحوَّل لنمط التراكب المنبثق داخل العرض (In-View Modal Overlay) بعد أنواع المصادر
النيترونية (الجولة 139/140). الفرق هذه المرة: `LocationsView` شاشة بسيطة ذات نموذج تحرير واحد داخل
`UserControl` واحد (لا نافذة `Window` مستقلة أصلاً)، لذا لم تكن هناك حاجة لإنشاء `UserControl` overlay
منفصل أو ViewModel فرعي إضافي كما حدث مع `NeutronSourceTypesOverlay.xaml`/
`NeutronTypesManagementViewModel` — كل ما تطلبه الأمر هو: (1) حذف شرط `Visibility` المرتبط بـ
`IsEditing` عن `Border` الجدول ليبقى ظاهراً دائماً، و(2) لف `Border` النموذج بخلفية معتّمة
(`#88000000`) وبطاقة مركزية بظل و`Panel.ZIndex="1000"` بنفس أسلوب الجولة 139، مع إبقاء كل
الـ Bindings/Commands الحالية (`SaveCommand`/`CancelEditCommand`) ونقل حقول النموذج حرفياً بلا أي
تعديل C#. لم يُلمس `LocationsViewModel.cs` إطلاقاً. أُضيف اختبار تكامل حقيقي
(`LocationsViewOverlayTests.cs`) يستضيف `LocationsView` داخل `Window` حقيقية فعلياً (`Show()`/
`UpdateLayout()`, وليس فقط `Measure`/`Arrange`) للتحقق من أن الجدول يبقى ظاهراً في الحالتين
(`IsEditing = true/false`) وأن التراكب (`Panel.ZIndex="1000"`) يظهر فقط عند `IsEditing = true`. تحقُّق
يدوي (بإعادة الملف مؤقتاً لنسخته القديمة من `git show` ثم استعادة النسخة المُصلَحة) أثبت أن كلا
الاختبارين يفشلان فعلاً (`لم يتم العثور على Grid التراكب`) بدون الإصلاح، وينجحان معه. 1150 اختباراً
محلياً (Debug)، صفر فشل، صفر تجاوز (+2 عن الجولة 139؛ الجولة 140 لم تُضِف اختباراً جديداً). بناء
المشروع الرئيسي بلا تحذيرات جديدة.

## 11. الجولة 142 — In-View Modal Overlay لشاشة النظائر المشعة (RadioisotopesView)

**الحالة: Draft PR غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس معاً — لا دمج قبل الاثنين.**

ثالث شاشة تُحوَّل لنمط التراكب المنبثق داخل العرض (In-View Modal Overlay)، إتماماً للمسار B بعد أنواع
المصادر النيترونية (الجولة 139/140) والمواقع (الجولة 141). `RadioisotopesView` تشترك نفس البنية:
ViewModel واحد بين المشهدين (لا ViewModel فرعي منفصل)، ونموذج تحرير من خطوتين (Stepper) داخل
`ScrollViewer`/`Border`. التعديل شمل فقط: (1) حذف شرط `Visibility` المرتبط بـ `IsEditing` عن `Grid`
الجدول ليبقى ظاهراً دائماً بغض النظر عن حالة التحرير، و(2) لف `Border` النموذج (الشبكة ذات 4 صفوف:
شريط العنوان، مؤشر الخطوات، محتوى الخطوة، أزرار التذييل) بخلفية معتّمة (`#88000000`) وبطاقة مركزية
ثابتة العرض (`720`) بظل و`Panel.ZIndex="1000"` بنفس أسلوب الجولتين 140/141، مع نقل مفاتيح `Enter`/
`Escape` من `ScrollViewer.InputBindings` إلى `Border.InputBindings` الخاصة بالبطاقة (بلا تكرار)،
وإبقاء كل الـ Bindings/Commands الحالية (`CurrentStep`, `NextStepCommand`/`PreviousStepCommand`/
`SaveCommand`/`CancelEditCommand`) ومحتوى الخطوتين حرفياً بلا أي تعديل C#. لم يُلمس
`RadioisotopesViewModel.cs` ولا `LeakTestsView.xaml` إطلاقاً. أُضيف اختبار تكامل حقيقي
(`RadioisotopesViewOverlayTests.cs`) يستضيف `RadioisotopesView` داخل `Window` حقيقية فعلياً
(`Show()`/`UpdateLayout()`, وليس فقط `Measure`/`Arrange`) للتحقق من أن الجدول يبقى ظاهراً في الحالتين
(`IsEditing = true/false`) وأن التراكب (`Panel.ZIndex="1000"`) يظهر فقط عند `IsEditing = true`. تحقُّق
يدوي (بإعادة `RadioisotopesView.xaml` مؤقتاً لنسخته القديمة عبر `git checkout <commit-قديم> --` ثم
استعادة النسخة المُصلَحة) أثبت أن كلا الاختبارين يفشلان فعلاً (`لم يتم العثور على Grid التراكب`) بدون
الإصلاح، وينجحان معه. 1153 اختباراً محلياً (Debug)، صفر فشل، صفر تجاوز (+2 عن الجولة 141). بناء
المشروع الرئيسي بلا تحذيرات جديدة.

## 12. الجولة 143 — In-View Modal Overlay لشاشة الاستعارة (BorrowView)

**الحالة: Draft PR غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس معاً — لا دمج قبل الاثنين.**

رابع شاشة تُحوَّل لنمط التراكب المنبثق داخل العرض (In-View Modal Overlay)، إتماماً للمسار B بعد أنواع
المصادر النيترونية (الجولة 139/140)، المواقع (الجولة 141)، والنظائر المشعة (الجولة 142).
`BorrowView` تشترك نفس البنية: ViewModel واحد بين مشهد القائمة ومشهد النموذج (لا ViewModel فرعي
منفصل)، ونموذج تحرير له وضعان متبادلان يحكمهما `IsNew` — وضع أ (طلب استعارة جديد، Stepper من
خطوتين) ووضع ب (عرض وتفاصيل وإرجاع). التعديل شمل فقط: (1) حذف شرط `Visibility` المرتبط بـ
`IsEditing` عن `StackPanel` أزرار الإجراءات العلوية (إضافة/تصدير) ليبقى ظاهراً دائماً، (2) حذف نفس
الشرط عن `Grid` مشهد القائمة (البطاقات الإحصائية، البحث والتصفية، الجدول) ليبقى ظاهراً دائماً بغض
النظر عن حالة التحرير، و(3) لف `Border` بطاقة النموذج (التي تحوي الشبكتين المتبادلتين حسب `IsNew`
دون أي تعديل عليهما) بخلفية معتّمة (`#88000000`) وبطاقة مركزية ثابتة العرض (`720`) بظل
و`Panel.ZIndex="1000"` بنفس أسلوب الجولات 140/141/142، دون إضافة أي `KeyBinding` جديد لم يكن موجوداً
(`Escape` فقط، كما كان). أُبقيت كل الـ Bindings/Commands الحالية (`AddNewCommand`, `CurrentStep`,
`NextStepCommand`/`PreviousStepCommand`/`SubmitCommand`/`MarkReturnedCommand`/`CancelEditCommand`)
ومحتوى الوضعين حرفياً بلا أي تعديل. لم يُلمس `BorrowViewModel.cs` إطلاقاً. أُضيف اختبار تكامل حقيقي
(`BorrowViewOverlayTests.cs`) يستضيف `BorrowView` داخل `Window` حقيقية فعلياً (`Show()`/
`UpdateLayout()`, وليس فقط `Measure`/`Arrange`) للتحقق من أن الجدول يبقى ظاهراً في الحالتين
(`IsEditing = true/false`) وأن التراكب (`Panel.ZIndex="1000"`) يظهر فقط عند `IsEditing = true`؛ لأن
`AddNewCommand` يستدعي `LoadAvailableSources()` التي تفتح `DbContext` فعلياً، استُخدم
`SqliteInMemoryFixture` حقيقي (بدلاً من الاعتماد الضمني على `App.ServiceProvider`) لضمان عدم تأثر
النتيجة بتلوث الحالة الساكنة بين الاختبارات عند تشغيل الحزمة كاملة. تحقُّق يدوي (بإعادة
`BorrowView.xaml` مؤقتاً لنسخته القديمة عبر `git stash` ثم استعادة النسخة المُصلَحة عبر
`git stash apply`) أثبت أن كلا الاختبارين يفشلان فعلاً (`لم يتم العثور على Grid التراكب`) بدون
الإصلاح، وينجحان معه. 1155 اختباراً محلياً (Debug)، صفر فشل، صفر تجاوز (+2 عن الجولة 142). بناء
المشروع الرئيسي بلا تحذيرات جديدة.

## 13. تصحيح معماري للجولة 143 — استبدال التراكب المنبثق داخل العرض بنافذة WPF حقيقية لشاشة الاستعارة (BorrowFormWindow)

**الحالة: Draft PR #37 غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس معاً — لا دمج قبل الاثنين.**

قرار معماري ألغى نمط التراكب المنبثق داخل العرض (In-View Modal Overlay) الذي طبّقته الجولة 143 على
`BorrowView` (والمُصحَّح موضعياً بعدها في نفس الفرع) واستبدله بنافذة WPF مستقلة حقيقية
(`BorrowFormWindow.xaml`/`.xaml.cs`) بنفس نمط `LocationDetailsWindow` القائم مسبقاً في المستودع
(شريط عنوان نظام التشغيل الأصلي، `WindowStartupLocation="CenterOwner"`، تُفتح عبر `ShowDialog()`).
لا يُلغي هذا القرار مبادرة التراكب المنبثق لبقية الشاشات (139/140/141/142) — يقتصر فقط على `BorrowView`
كتصحيح لاحق. التعديل: (1) حُذف `Grid` التراكب بالكامل (`Panel.ZIndex="1000"`، الخلفية المعتّمة
`#88000000`، البطاقة المركزية بالظل) من `BorrowView.xaml`، مع بقاء "المشهد 1" (البطاقات الإحصائية،
شريط البحث والتصفية، الجدول) ظاهراً دائماً كما كان بلا أي `Visibility` مرتبط بـ`IsEditing`؛ (2) أُنشئت
`BorrowFormWindow.xaml`/`.xaml.cs` تحوي *محتوى* البطاقة السابقة حرفياً بلا أي تعديل على الربط أو
الأوامر (`ScrollViewer` وما بداخله: وضعا `IsNew` — طلب استعارة جديد Stepper من خطوتين، وعرض/تفاصيل/
إرجاع)، مع `KeyBinding` وحيد لـ`Escape` على `Window.InputBindings` (نفس السلوك السابق)، ودون
`DataContext` في XAML — يُمرَّر من كود `BorrowView.xaml.cs`؛ (3) أُضيف مفتاح ترجمة جديد
`TitleBorrowForm` ("نموذج الاستعارة" / "Borrow Form") في `Strings.ar.xaml`/`Strings.en.xaml` بنفس نمط
`TitleLocationDetails`؛ (4) أُضيف منطق دورة حياة نافذة في `BorrowView.xaml.cs` (طبقة العرض حصراً، بلا
لمس `BorrowViewModel.cs`): اشتراك بـ`PropertyChanged` على `DataContext` بعد `Loaded` وإلغاء الاشتراك
عند `Unloaded`؛ عند تحوّل `IsEditing` إلى `true` (وبحارس ضد إعادة الدخول يمنع فتح نافذة ثانية) تُنشأ
`BorrowFormWindow` بـ`DataContext`/`Owner` مناسبين وتُستدعى `ShowDialog()`؛ وعند تحوّلها إلى `false`
(من `Save`/`Submit`/`MarkReturned`/`Cancel` داخل الـViewModel كما كانت) تُغلَق النافذة إن كانت مفتوحة.
لم يُلمس `BorrowViewModel.cs` ولا أي أمر من أوامره (`AddNewCommand`, `SubmitCommand`,
`CancelEditCommand`, `NextStepCommand`, `PreviousStepCommand`, `MarkReturnedCommand`) إطلاقاً. أُعيدت
كتابة `BorrowViewOverlayTests.cs` بالكامل: اختبار أول يثبت أن الجدول ظاهر دائماً بغض النظر عن
`IsEditing`؛ واختبار ثانٍ يثبت أن `AddNewCommand` يفتح فعلياً نافذة من نوع `BorrowFormWindow` (يُتحقَّق
عبر `Application.Current.Windows`) وأن `CancelEditCommand` يُغلقها. **تسوية اختبارية موثَّقة:**
`ShowDialog()` التي يستدعيها `BorrowView.xaml.cs` عند `IsEditing=true` تحجب مسار التنفيذ الحالي بمضخة
رسائل متداخلة (nested message pump) خاصة بها؛ استُخدم `Dispatcher.CurrentDispatcher.BeginInvoke` مع
`DispatcherPriority.ApplicationIdle` لجدولة التحقق من فتح النافذة واستدعاء `CancelEditCommand` بحيث
تُنفَّذ هذه الخطوة أثناء تشغيل حلقة `ShowDialog()` المتداخلة نفسها، فتُغلَق النافذة ويعود
`AddNewCommand.Execute` من الحجب طبيعياً — بدل خيط STA مخصص إضافي، لأن `WpfStaFixture` القائم يوفر
بالفعل خيط STA واحد بمضخة `Dispatcher.Run()` تكفي لتشغيل `BeginInvoke` أثناء `ShowDialog()` المتداخلة.
**تحقُّق يدوي إلزامي:** عبر `git stash push -u` (بمعرّف فريد، واستعادة بـ`git stash apply <sha>` لا
`pop`) أُعيدت ملفات `BorrowView.xaml`/`.xaml.cs`/`BorrowFormWindow.*`/ملفي الترجمة إلى نسختها السابقة
للتصحيح بينما بقي ملف الاختبار المُعاد كتابته كما هو — فشل البناء فعلياً بخطأ ترجمة
(`CS0246: BorrowFormWindow could not be found`) لأن النوع غير موجود قبل التصحيح، ثم استُعيدت الملفات
المُصحَّحة عبر `git stash apply` ونجح كلا الاختبارين. 1155 اختباراً محلياً (Debug)، صفر فشل، صفر
تجاوز (عدد صافٍ ثابت — اختباران استُبدلا باختبارين). بناء المشروع الرئيسي بلا تحذيرات جديدة (نفس
التحذيرات الخمسة السابقة في `LoginWindow.xaml.cs`/`ViewInstantiationTests.cs`، غير متعلقة بالاستعارة).

### 13.1 إصلاح عاجل لاحق — تصفير `IsEditing` تلقائياً عند إغلاق `BorrowFormWindow` عبر ✕/Alt+F4

عطل حرج اكتُشف بالاختبار البصري الفعلي بعد التصحيح المعماري أعلاه: إغلاق `BorrowFormWindow` عبر زر
الإغلاق الأصلي لنظام التشغيل أو `Alt+F4` يستدعي فقط `Window.Close()` الافتراضي لـWPF، ولا يمر أبداً
عبر `CancelEditCommand`، فتبقى `BorrowViewModel.IsEditing` عالقة على `true` — يمنع فتح أي نافذة جديدة
لاحقاً ويُظهر تحذير "نافذة مفتوحة" عند التنقل أو إغلاق التطبيق، يضطر المستخدم معه لإنهاء العملية بالقوة.
الإصلاح: أُضيف معالج `Closing` في `BorrowFormWindow.xaml.cs` يستدعي `CancelEditCommand` (بشرط
`CanExecute`) فقط إن كانت `IsEditing` لا تزال `true` لحظة الإغلاق (أي إغلاق عبر ✕/Alt+F4، لا نجاح
الحفظ الذي يُصفّرها الـViewModel نفسه أولاً) — عبر نفس مسار الإلغاء اليدوي تماماً، بلا لمس
`BorrowViewModel.cs`. أُضيفت خاصية `IsClosingInProgress` لمنع استدعاء `Close()` متكرر (reentrant) من
`BorrowView.xaml.cs` أثناء معالجة `Closing` نفسها. اختبار انحداري جديد
(`BorrowFormWindow_ClosedViaNativeCloseButton_ResetsIsEditing_AndAllowsReopening`) يحاكي السيناريو
تماماً: `AddNewCommand` ثم `Close()` مباشرة على كائن النافذة (لا `CancelEditCommand`)، يتحقق من عودة
`IsEditing` إلى `false` تلقائياً، ثم يتحقق أن `AddNewCommand` التالي يفتح نافذة جديدة فعلياً لا شيء.
تحقُّق يدوي مستقل (بإعادة `BorrowFormWindow.xaml.cs`/`BorrowView.xaml.cs` مؤقتاً للنسخة السابقة عبر
`git checkout <commit> --`) أثبت فشل الاختبار فعلاً (`Assert.False(): Actual: True`) قبل الإصلاح
ونجاحه بعده. طلب ثانوي في نفس الكوميت: توسيع `BorrowFormWindow` (720×720 → 900×860) لتقليل الحاجة
للتمرير، بلا إعادة تصميم تخطيط الحقول. مسار "تفاصيل وإجراء الاستعارة" (زر العين) يستخدم نفس خاصية
`IsEditing` وذات آلية الفتح/الإغلاق في `BorrowView.xaml.cs`، فيستفيد من الإصلاح تلقائياً دون حاجة لمسار
منفصل. 1156 اختباراً محلياً (Debug، +1)، صفر فشل. بناء بلا تحذيرات جديدة.

## 14. الجولة 144 — تصحيح معماري لأنواع المصادر النيترونية: استبدال التراكب المنبثق داخل العرض بنافذة WPF حقيقية (`NeutronSourceTypesWindow`)

**الحالة: Draft PR غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس معاً — لا دمج قبل الاثنين.**

قرار معماري طبّق على `NeutronSourceTypesOverlay` (أول شاشة حُوِّلت لنمط التراكب المنبثق داخل العرض،
الجولة 139/140) نفس التصحيح الذي طُبِّق على `BorrowView` في البند 13 أعلاه — إلغاء نمط التراكب
واستبداله بنافذة WPF مستقلة حقيقية (`NeutronSourceTypesWindow.xaml`/`.xaml.cs`) بنفس نمط
`LocationDetailsWindow`/`BorrowFormWindow` القائمين (شريط عنوان نظام التشغيل الأصلي،
`WindowStartupLocation="CenterOwner"`، تُفتح عبر `ShowDialog()`). لا يُلغي هذا القرار مبادرة التراكب
المنبثق لبقية الشاشات المحوَّلة (141/142) — يقتصر فقط على شاشة إدارة أنواع المصادر النيترونية
المرجعية كتصحيح لاحق. التعديل: (1) حُذف `Grid` التراكب بالكامل (`Panel.ZIndex="1000"`، الخلفية
المعتّمة `#88000000`، `<views:NeutronSourceTypesOverlay>` وربط `xmlns:views`) من `SourcesView.xaml`،
مع بقاء جدول/بطاقات المصادر ظاهراً دائماً كما كان بلا أي `Visibility` مرتبط بحالة الإدارة؛ (2) حُذف
`NeutronSourceTypesOverlay.xaml`/`.xaml.cs` بالكامل بعد تأكيد خلوّ المستودع من أي مرجع كودي متبقٍ
لهما؛ (3) أُعيد إنشاء `NeutronSourceTypesWindow.xaml`/`.xaml.cs` (كانت قد حُذفت في الجولة 139) تحوي
*محتوى* التراكب السابق حرفياً بلا أي تعديل على الربط أو الأوامر، بإعادة استخدام مفتاحي الترجمة
القائمين `TitleNeutronSourceTypes`/`SubtitleNeutronTypes` دون أي مفتاح جديد، مع `KeyBinding` وحيد
لـ`Escape` على `Window.InputBindings` (نفس السلوك السابق)، ودون `DataContext` في XAML — يُمرَّر من
كود `SourcesView.xaml.cs`؛ لا حاجة لتسجيل `NeutronSourceTypesWindow` في حاوية DI بـ`App.xaml.cs` (تُنشأ
مباشرة بـ`new` بنفس نمط `BorrowFormWindow`، لا `LocationDetailsWindow`)؛ (4) أُضيف منطق دورة حياة
نافذة في `SourcesView.xaml.cs` (طبقة العرض حصراً، بلا لمس `SourcesViewModel.cs`/
`NeutronSourceTypesViewModel.cs`): اشتراك بـ`PropertyChanged` على `DataContext` بعد `Loaded` وإلغاء
الاشتراك عند `Unloaded`؛ عند تحوّل `IsManagingNeutronTypes` إلى `true` (وبحارس ضد إعادة الدخول يمنع
فتح نافذة ثانية) تُنشأ `NeutronSourceTypesWindow` بـ`DataContext`/`Owner` مناسبين وتُستدعى
`ShowDialog()`؛ وعند تحوّلها إلى `false` تُغلَق النافذة إن كانت مفتوحة وليست بصدد الإغلاق أصلاً
(`IsClosingInProgress`)؛ (5) أُضيف معالج `Closing` في `NeutronSourceTypesWindow.xaml.cs` يستدعي
`CloseCommand` (بشرط `CanExecute`) مباشرة — بلا حاجة لفحص حالة وسيطة كما في `BorrowFormWindow` لأن
`CloseCommand` هنا مُعرَّف أصلاً منذ الجولة 139 كمسار إغلاق وحيد (`OnClose` + تصفير
`IsManagingNeutronTypes`) — مع خاصية `IsClosingInProgress` لمنع استدعاء `Close()` متكرر
(reentrant) من `SourcesView.xaml.cs`، فتُطبَّق دفعة واحدة معالجة الإغلاق عبر ✕/Alt+F4 التي احتاجت
إصلاحاً عاجلاً لاحقاً في حالة `BorrowFormWindow` (البند 13.1)، بدل تكرار نفس العطل ثم إصلاحه لاحقاً.
لم يُلمس `NeutronSourceTypesViewModel.cs` ولا أي أمر من أوامره (`CloseCommand` وأوامر الحفظ/التحقق
القائمة) إطلاقاً. أُعيدت كتابة اختبار التكامل بالكامل: حُذف الاختبار القديم
`SourcesView_ByDefault_NeutronTypesOverlayHostIsCollapsed` من `ViewInstantiationTests.cs` (كان
يتحقق من عنصر `NeutronSourceTypesOverlay` الذي لم يعد موجوداً في `SourcesView.xaml`)، وأُضيف ملف
اختبار تكامل جديد (`SourcesViewNeutronOverlayTests.cs`) يستضيف `SourcesView` داخل `Window` حقيقية
فعلياً (`Show()`/`UpdateLayout()`) بثلاثة اختبارات: (أ) جدول المصادر ظاهر دائماً بغض النظر عن
`IsManagingNeutronTypes`، (ب) `OpenNeutronSourceTypesManagementCommand` يفتح فعلياً نافذة من نوع
`NeutronSourceTypesWindow` (يُتحقَّق عبر `Application.Current.Windows`) وأن جدول المصادر يبقى ظاهراً
تحتها، و(ج) الإغلاق عبر ✕/Alt+F4 (محاكاة بـ`Close()` مباشرة لا `CloseCommand`) يُصفِّر
`IsManagingNeutronTypes` تلقائياً ويسمح بإعادة الفتح لاحقاً. **تسوية اختبارية موثَّقة (نفس أسلوب
البند 13):** `ShowDialog()` التي يستدعيها `SourcesView.xaml.cs` عند `IsManagingNeutronTypes=true`
تحجب مسار التنفيذ الحالي بمضخة رسائل متداخلة خاصة بها؛ استُخدم `Dispatcher.CurrentDispatcher.BeginInvoke`
مع `DispatcherPriority.ApplicationIdle` لجدولة التحقق من فتح النافذة واستدعاء `CloseCommand`/`Close()`
بحيث تُنفَّذ هذه الخطوة أثناء تشغيل حلقة `ShowDialog()` المتداخلة نفسها. **عطل إضافي اكتُشف ثم أُصلح
أثناء كتابة الاختبار الأول (لا علاقة له بالتحويل المعماري نفسه):** `SourcesViewModel` يُحمِّل بياناته
بشكل غير متزامن في مُنشئه (`_ = LoadDataAsync();` تنتظر `Task.Run(...)` داخلياً)، ودالة `LoadDataAsync`
تستدعي `_sourceService.GetDeletedSources()` مباشرة بعد `GetAllSources()` وتستخدم النتيجة فوراً
(`deletedList.Count`) دون فحص `null`؛ إعداد الاختبار (`CreateViewModel`) لم يكن يُهيّئ Stub لهذه
الدالة على `Mock<ISourceService>`، فأعادت Moq افتراضياً `null` (سلوك `MockBehavior.Loose` مع نوع
مرجعي)، فرُمي `NullReferenceException` غير مُلتقَط داخل المهمة غير المتزامنة قبل الوصول لسطر
`Sources = new ObservableCollection<Source>(allSources);`، فبقيت `Sources` فارغة دائماً وظهر جدول
المصادر (`SourceCardsPanel`) كـ`Collapsed` خطأً — ظاهره سباق زمني (Dispatcher لم يُفرَّغ بعد) لكن
جذره الفعلي إعداد Mock ناقص في الاختبار نفسه، لا خلل في كود الإنتاج ولا في ربط `Visibility`. أُضيف
Stub الناقص (`mockSourceService.Setup(s => s.GetDeletedSources()).Returns(new List<Source>())`) في
`CreateViewModel()`، إلى جانب مساعد استطلاع (`WaitForSourcesLoaded`) يُفرِّغ طابور Dispatcher دورياً
حتى تكتمل `Sources` (بمهلة قصوى ~2 ثانية) بدل الاعتماد على توقيت تفريغ واحد غير مضمون، لتفادي سباق
حقيقي متبقٍ في توقيت اكتمال `Task.Run` نفسه. لم يُعدَّل أي توكيد (`Assert`) لإخفاء هذا العطل. **تحقُّق
يدوي إلزامي:** عبر `git stash push -u` (بمعرّف فريد، واستعادة بـ`git stash apply <sha>` لا `pop`) أُعيد
تخزين ملفات الشاشة (`SourcesView.xaml`/`.xaml.cs`، ملفي `NeutronSourceTypesOverlay.*` المحذوفين،
ملفي `NeutronSourceTypesWindow.*` الجديدين، وتعديل `ViewInstantiationTests.cs`) في stash بينما بقي
ملف الاختبار الجديد كما هو في مجلد العمل؛ فشل البناء فعلياً بأخطاء ترجمة متعددة
(`CS0246: NeutronSourceTypesWindow could not be found`) لأن النوع غير موجود قبل التصحيح، ثم
استُعيدت الملفات المُصحَّحة عبر `git stash apply <sha>` وأُسقطت النسخة المخزَّنة بعدها
(`git stash drop`)، ونجحت الاختبارات الثلاثة معاً. 1158 اختباراً محلياً (Debug)، صفر فشل، صفر تجاوز
(−1 اختبار قديم حُذف، +3 اختبارات جديدة = +2 صافياً عن الجولة 143). بناء المشروع الرئيسي بلا تحذيرات
جديدة (نفس التحذيرات الثلاثة المسبقة: اثنان CS8604 في `LoginWindow.xaml.cs`، وواحد CS8604 في
`ViewInstantiationTests.cs`، غير متعلقة بهذه الجولة). لم يُلمس `App.xaml.cs` إطلاقاً (بخلاف الجولة
139 التي احتاجت حذف تسجيل DI، لا حاجة هنا لأن `NeutronSourceTypesWindow` تُنشأ مباشرة بـ`new` لا عبر
الحاوية).

## 15. الجولة 145 — استبدال التراكب المنبثق داخل العرض بنافذة WPF حقيقية لشاشة المواقع (LocationFormWindow)

**الحالة: Draft PR غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس.**

استكمالاً لنفس التصحيح المعماري المطبَّق على `BorrowView` في الجولة 143 (وبتضمين درس الجولة 144 حول
شريط العنوان)، طُبِّق نفس النمط على شاشة المواقع: أُلغي تراكب `LocationsView.xaml` المنبثق داخل العرض
(`Panel.ZIndex="1000"`، الخلفية المعتّمة `#88000000`، البطاقة المركزية) المُضاف في الجولة 141، واستُبدل
بنافذة WPF مستقلة حقيقية (`LocationFormWindow.xaml`/`.xaml.cs`) بنفس بنية `BorrowFormWindow`:
`WindowStyle="SingleBorderWindow"` و`ShowInTaskbar="True"` صريحتان (بدل الاعتماد على القيم الافتراضية
— نفس الدرس الذي تسبب في إصلاح الجولة 144 العاجل لـ`NeutronSourceTypesWindow`/`BorrowFormWindow`)،
`WindowStartupLocation="CenterOwner"`، تُفتح عبر `ShowDialog()`. `LocationsViewModel` لا تملك نموذج
عرض فرعي منفصل (`IsEditing`/`AddNewCommand`/`EditCommand`/`SaveCommand`/`CancelEditCommand`/
`EditName`/`EditType`/`EditBuilding`/`EditRoom`/`EditPerson` كلها مباشرة على `LocationsViewModel` نفسها
— بنفس بنية `BorrowViewModel`/`BorrowFormWindow`)؛ لذا `DataContext` النافذة الجديدة هو نفس نسخة
`LocationsViewModel` الممرَّرة من `LocationsView.xaml.cs`، لا نموذج فرعي جديد. التعديل: (1) حُذف
`Grid` التراكب بالكامل من `LocationsView.xaml`، مع بقاء الجدول (`Border`/`DataGrid`) ظاهراً دائماً
دون أي `Visibility` مرتبط بـ`IsEditing`، وبُسِّط `Grid Grid.Row="2"` الخارجي إلى `Border Grid.Row="2"`
مباشرة بعد أن أصبح يحوي طفلاً واحداً فقط (بلا أي تغيير على أعمدة/أوامر `DataGrid`)؛ (2) أُنشئت
`LocationFormWindow.xaml`/`.xaml.cs` تحوي محتوى البطاقة السابقة حرفياً بلا أي تعديل على الربط أو
الأوامر (`EditName`/`EditType`/`EditBuilding`/`EditRoom`/`EditPerson`/`SaveCommand`/
`CancelEditCommand`)، مع حذف زر الإغلاق الدائري ✕ المكرر من رأس البطاقة (الإغلاق فقط عبر شريط العنوان
الأصلي، `Escape`، أو زر "إلغاء")، وتبسيط الرأس إلى `TextBlock` عنوان واحد يعيد استخدام مفتاح الترجمة
القائم `LocationDataTitle` (بلا مفتاح جديد)؛ نظراً لأن `SaveCommand` هنا إجراء حفظ وحيد غير مبهم (بخلاف
Stepper الاستعارة ثنائي الخطوات)، رُبط كل من `Escape`→`CancelEditCommand` و`Return`→`SaveCommand` على
`Window.InputBindings`؛ (3) لا مفتاح ترجمة جديد؛ (4) أُعيدت كتابة `LocationsView.xaml.cs` بنفس منطق
دورة حياة النافذة في `BorrowView.xaml.cs` (اشتراك `PropertyChanged` بعد `Loaded`، إلغاء عند `Unloaded`،
حارس ضد فتح نافذة ثانية، وحارس `IsClosingInProgress` لمنع `Close()` متكرر). لم يُلمس
`LocationsViewModel.cs` إطلاقاً. استُبدل `LocationsViewOverlayTests.cs` (الجولة 141، يتحقق من نمط
التراكب القديم غير الموجود بعد الآن) بملف جديد `LocationsFormWindowTests.cs` بنفس منهجية
`BorrowViewOverlayTests.cs`: (أ) الجدول ظاهر دائماً بغض النظر عن `IsEditing`، (ب) `AddNewCommand`
يفتح فعلياً نافذة `LocationFormWindow` (يُتحقَّق عبر `Application.Current.Windows` وتطابق
`DataContext`)، و(ج) إغلاق النافذة مباشرة عبر `Close()` (محاكاة ✕ الأصلي) يُصفِّر `IsEditing` تلقائياً
ويسمح بإعادة الفتح لاحقاً — بنفس أسلوب جدولة `Dispatcher.CurrentDispatcher.BeginInvoke` مع
`DispatcherPriority.ApplicationIdle` أثناء حلقة `ShowDialog()` المتداخلة. **تحقُّق تجريبي حسب ترتيب
التنفيذ (بلا `git stash`):** كُتب ملف الاختبار الجديد أولاً مقابل `LocationsView`/`LocationsView.xaml.cs`
غير المعدَّلة، فشل البناء فعلياً بخطأ ترجمة (`CS0246: LocationFormWindow could not be found`) لأن النوع
غير موجود قبل التصحيح؛ ثم نُفِّذ التصحيح كاملاً، ونجحت الاختبارات الثلاثة. 1157 اختباراً محلياً (Debug)،
صفر فشل، صفر تجاوز (1156 + 3 اختبارات جديدة − 2 اختبارا `LocationsViewOverlayTests.cs` المحذوفَين).
بناء المشروع الرئيسي بلا تحذيرات جديدة (نفس التحذيرات الأربعة السابقة في `LoginWindow.xaml.cs`، غير
متعلقة بالمواقع). لا انحرافات عن عقد الجولة 145.

---

## 16. الجولة 146 — استبدال التراكب المنبثق داخل العرض بنافذة WPF حقيقية لشاشة النظائر المشعة (RadioisotopeFormWindow)

**الحالة: Draft PR غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس.**

استكمال المسار المعماري الذي طبَّقته الجولتان 143 (`BorrowFormWindow`) و145 (`LocationFormWindow`) على
آخر شاشة متبقية بنمط التراكب المنبثق: `RadioisotopesView`. أُلغي تراكب `RadioisotopesView.xaml` المنبثق
داخل العرض (`Panel.ZIndex="1000"`، الخلفية المعتّمة `#88000000`، البطاقة المركزية بعرض `720`) المُضاف في
الجولة 142، واستُبدل بنافذة WPF مستقلة حقيقية (`RadioisotopeFormWindow.xaml`/`.xaml.cs`) بنفس بنية
`LocationFormWindow`: `WindowStartupLocation="CenterOwner"`، `WindowStyle="SingleBorderWindow"`،
`ShowInTaskbar="True"` صريحتان، تُفتح عبر `ShowDialog()`. التعديل: (1) حُذف `Grid` التراكب بالكامل من
`RadioisotopesView.xaml` (المشهد 1 — الجدول وشريط البحث — يبقى ظاهراً دائماً كما كان، بلا أي تغيير)، مع
حذف موارد `BoolToVisibilityConverter`/`InverseBoolToVisibilityConverter` من `UserControl.Resources`
بعد أن أصبحا بلا أي استهلاك في الملف؛ (2) أُنشئت `RadioisotopeFormWindow.xaml`/`.xaml.cs` تحوي محتوى
نموذج الخطوتين (Stepper) حرفياً بلا أي تعديل على الربط أو الأوامر (`EditSymbol`/`EditRadiationType`/
`EditName`/`EditArabicName`/`EditHalfLifeText`/`EditHalfLifeUnit`/`EditEnergyText`/`EditYieldText`/
`EditGammaConstantText`/`EditNotes`/`EditEnglishNotes`/`CurrentStep`/`NextStepCommand`/
`PreviousStepCommand`/`SaveCommand`/`CancelEditCommand`/`SuggestGammaConstantCommand`)، مع حذف زر
الإغلاق الدائري ✕ المكرر من رأس النموذج (الإغلاق فقط عبر شريط العنوان الأصلي، `Escape`، أو زر "إلغاء")؛
(3) مفتاح ترجمة جديد واحد `TitleRadioisotopeForm` (عنوان النافذة نفسها) أُضيف لكلا القاموسين — بقية
عناوين النموذج (`AddRadioisotopeDataTitle`/`EditRadioisotopeDataTitle`) أُعيد استخدامها كما هي دون
تعديل؛ (4) أُعيدت كتابة `RadioisotopesView.xaml.cs` (كانت فارغة تماماً قبل هذه الجولة، بلا أي منطق
دورة حياة) بنفس منطق `LocationsView.xaml.cs`/`BorrowView.xaml.cs` (اشتراك `PropertyChanged` بعد
`Loaded`، إلغاء عند `Unloaded`، حارس ضد فتح نافذة ثانية، وحارس `IsClosingInProgress` لمنع `Close()`
متكرر). لم يُلمس `RadioisotopesViewModel.cs` إطلاقاً. **قرار مُثبَّت صريحاً (لا انحراف):** أُضيف
`KeyBinding Key="Return" Command="{Binding SaveCommand}"` على `Window.InputBindings` — بخلاف
`BorrowFormWindow` التي تجاهلت هذا الربط لتعدد أوامر الحفظ بين خطواتها، هنا يوجد أمر `SaveCommand`
واحد لا لبس فيه يُستدعى من كل خطوة (الزر المرتبط به يظهر فقط في الخطوة 2 عبر `Visibility`، لكن الأمر
نفسه هو ذاته دون تفريع)، وهذا مطابق حرفياً لسلوك التراكب القديم الذي كان يحمل هذا الـKeyBinding نفسه
منذ الجولة 142 دون مشكلة. استُبدل `RadioisotopesViewOverlayTests.cs` (الجولة 142، يتحقق من نمط التراكب
القديم غير الموجود بعد الآن) بملف جديد `RadioisotopeFormWindowTests.cs` بنفس منهجية
`LocationsFormWindowTests.cs`: (أ) الجدول ظاهر دائماً بغض النظر عن `IsEditing`، (ب) `AddNewCommand`
يفتح فعلياً نافذة `RadioisotopeFormWindow` (يُتحقَّق عبر `Application.Current.Windows` وتطابق
`DataContext`)، و(ج) إغلاق النافذة مباشرة عبر `Close()` (محاكاة ✕ الأصلي) يُصفِّر `IsEditing` تلقائياً
ويسمح بإعادة الفتح لاحقاً — بنفس أسلوب جدولة `Dispatcher.CurrentDispatcher.BeginInvoke` مع
`DispatcherPriority.ApplicationIdle` أثناء حلقة `ShowDialog()` المتداخلة. **تحقُّق تجريبي إلزامي عبر
`git stash push -u`/`git stash apply <sha>` (لا `pop`):** أُوقفت جميع ملفات الإنتاج (`RadioisotopeFormWindow.xaml`/`.xaml.cs`،
`RadioisotopesView.xaml`/`.xaml.cs`، `Strings.ar.xaml`/`Strings.en.xaml`) بينما بقي ملف الاختبار الجديد
كما هو؛ فشل البناء فعلياً بخطأ ترجمة (`CS0246: RadioisotopeFormWindow could not be found`) على الكود
السابق للتصحيح، ثم استُعيدت ملفات الإصلاح كاملة (`apply` لا `pop`، والمُدخَل حُذف بعدها بـ`drop`)، ونجحت
الاختبارات الثلاثة. **قياس أساس منفصل (baseline) أُجري بنفس الأسلوب** للتأكد من صافي عدد الاختبارات:
1159 اختباراً محلياً (Debug) على حالة المستودع قبل هذه الجولة (تشمل تصحيح وحدات النشاط في PR #40)، صعوداً
إلى 1160/1158 اختباراً (Debug/Release) بعد الجولة 146 — صافي +1 (اختباران قديمان حُذفا +ثلاثة اختبارات
جديدة أُضيفت). صفر فشل، صفر تجاوز. بناء المشروع الرئيسي بلا تحذيرات جديدة (نفس التحذيرات الخمس السابقة
في `LoginWindow.xaml.cs`/`ViewInstantiationTests.cs`، غير متعلقة بالنظائر المشعة). لا انحرافات عن عقد
الجولة 146 غير القرار المُثبَّت أعلاه بخصوص `KeyBinding Key="Return"`.
**انحراف عملية موثَّق (بيئي، يكرر نمط الجولة 128):** فشل تشغيل `round-implementer` كوكيل فرعي مرتين
متتاليتين (توقف كامل — Agent stalled — بلا أي تقدّم فعلي، عدم إنشاء أي ملف أو commit في كلتا
المحاولتين) أثناء العمل داخل نفس فرع/worktree الجلسة القائدة نفسها — بيئة عزل الوكيل الفرعي (sandbox)
عن مسارات الـworktree غير المخصَّصة له يبدو أنها السبب المحتمل، بلا تأكيد قطعي. نُفِّذت الجولة كاملة
مباشرة من جلسة القائد (تصميم، تنفيذ، تحقُّق TDD مزدوج بـ`git stash`، بناء واختبار كامل مرتين) بدل
التفويض لـ`round-implementer`.

---

## 17. الجولة 148 — استبدال التراكب المنبثق داخل العرض بنافذة WPF حقيقية لمعالج المصدر (SourceFormWindow)

**الحالة: Draft PR #43 غير مدموج، بانتظار قراءة القائد للكود ثم التحقق البصري الفعلي من إدريس.**

استكمال المسار المعماري للجولات 143/144/145/146/147 على آخر شاشة متبقية بنمط التراكب المنبثق:
معالج إضافة/تعديل المصدر في `SourcesView`. أُلغي التراكب (`ScrollViewer` مربوط بـ`IsEditing`، الأسطر
1071–2171 من `SourcesView.xaml`) واستُبدل بنافذة WPF مستقلة حقيقية (`SourceFormWindow.xaml`/`.xaml.cs`)
بنفس بنية `BorrowFormWindow`: `WindowStartupLocation="CenterOwner"`، `WindowStyle="SingleBorderWindow"`،
`ShowInTaskbar="True"`، `ResizeMode="CanResize"`، `FlowDirection="RightToLeft"`، تُفتح عبر `ShowDialog()`.

**اكتشاف بنيوي يخالف افتراض عقد الجولة (أُبلغ إدريس قبل أي تنفيذ، واعتُمد القرار بموافقته الصريحة):**
افترض العقد الأصلي وجود **معالجَين منفصلين** (عادي + نيتروني) يُحوَّل أحدهما في الجولة 148 ويُؤجَّل
الآخر للجولة 149. أثبت الفحص الفعلي عكس ذلك: الشاشة تستخدم **معالجاً واحداً** متعدد الخطوات ذا مفتاح
تبديل داخلي `IsNeutronForm`. الاختلاف بين النوعين محصور في **الخطوة 2 وحدها** (بطاقة نيترونية
`Visibility=IsNeutronForm` مقابل كتلة عادية `Visibility=!IsNeutronForm`)، بينما شريط العنوان ومؤشر
الخطوات والخطوة 1 والخطوة 3 وشريط الأزرار **متطابقة بالكامل**؛ كما أن `IsEditing` و`CurrentStep`
و`SaveCommand` و`CancelEditCommand` و`NextStepCommand`/`PreviousStepCommand` **كلها مشتركة**،
و`SaveAsync` دالة واحدة تتفرّع داخلياً بـ`if (IsNeutronForm)`. لذلك نُقل المعالج كوحدة واحدة إلى نافذة
واحدة مربوطة بـ`IsEditing` مباشرة. البديل (نافذتان منفصلتان) كان سيتطلب تكرار ~500 سطر XAML متطابق،
**وحذف راديو اختيار الفئة** من الخطوة 1 (تغيير UX مرئي)، وتبديل نافذة حيّة من داخل مضخة رسائل
`ShowDialog()` المتداخلة (خطر re-entrancy حقيقي). **بذلك تغطي هذه الجولة كامل الشاشة ولا حاجة لجولة
149 منفصلة.**

**`SourcesViewModel.cs` لم يُمسّ إطلاقاً** — لا منطق عمل، ولا خصائص جديدة، ولا خصائص مشتقة، ولا أوامر
(`git diff main...HEAD -- Sources-System-Project/ViewModels/SourcesViewModel.cs` فارغ). تحقّق منه
القائد ثم `change-verifier` مستقلاً.

**أمانة النقل:** المحتوى منقول حرفياً؛ `diff` بين الأصل والمنقول يُظهر **أربعة فروق مقصودة فقط**:
(1) إضافة `ScrollViewer` كجذر للنافذة؛ (2) `Margin="0,0,0,24"` ← `Margin="0"`؛ (3) حذف زر الإغلاق
الدائري ✕ المكرر من رأس النموذج (النافذة توفّره أصلاً) وتقليص أعمدة شبكة العنوان من 3 إلى 2؛
(4) تحويل **ثلاثة** روابط `RelativeSource` من `AncestorType=UserControl` إلى `AncestorType=Window`.
الفرق الرابع **ضروري لا تجميلي**: قوالب خلايا جدول النظائر المتعددة تصل إلى `DataContext.Radioisotopes`
و`DataContext.ActivityUnits` و`DataContext.RemoveIsotopeEntryCommand` عبر سلف `UserControl` لم يعد
موجوداً في شجرة النافذة — لولا تعديله **لفشلت هذه الروابط صامتةً بلا أي خطأ بناء**. تحقّق
`change-verifier` مستقلاً من عدم وجود أي رابط آخر معتمد على سلف لم يعد يُحلّ داخل النافذة.

**قرار مُثبَّت بخصوص `KeyBinding`:** رُبط `Escape`→`CancelEditCommand` فقط، ولم يُضَف ربط
`Return`/`Enter` إطلاقاً — استثناء متعمَّد للنماذج متعددة الخطوات مطابق لقرار الجولة 143
(`BorrowFormWindow`)، وبخلاف قرار الجولة 146 (`RadioisotopeFormWindow`) حيث كان `SaveCommand` واحداً
لا لبس فيه. يُلاحَظ أن التراكب القديم **كان** يربط `Enter`→`SaveCommand`، مما كان يسمح بتنفيذ الحفظ
من الخطوة 1 قبل اكتمال باقي الخطوات؛ إسقاط هذا الربط تصحيح مقصود وليس انحداراً صامتاً.

**اختبار قديم مُرحَّل لا محذوف:** فشل `ViewInstantiationTests.SourcesView_WhenActivelyBorrowed_DisablesStatusAndLocationComboBoxes`
بعد النقل لأنه يبحث في شجرة `SourcesView` المرئية عن قائمتَي "الحالة" (الخطوة 1) و"الموقع" (الخطوة 3)
اللتين انتقلتا إلى النافذة. التزاماً بقاعدة عدم تعديل اختبار فاشل لإنجاحه قبل تفسير سبب فشله، رُحِّل
السيناريو كاملاً إلى `SourceFormWindowTests` بنفس التأكيدات حرفياً (وجود قائمة معطَّلة واحدة على الأقل،
و`ToolTipService.GetShowOnDisabled` على كل قائمة معطَّلة) لكن على النافذة الحقيقية؛ لم يُضعَّف أي تأكيد.
سقط منه فقط كود حفظ لقطات PNG إلى مسار شخصي مُصلَّب (`C:\Users\DELL\.gemini\...`) لا علاقة له بالتأكيدات.
كما حُذفت الدالة المساعدة `FindVisualChildren` من `ViewInstantiationTests.cs` بعد أن صارت بلا مستدعٍ.

**ملف اختبار جديد** `SourceFormWindowTests.cs` (7 اختبارات) بنفس منهجية الجولات 145–147
(`Dispatcher.CurrentDispatcher.BeginInvoke` بأولوية `ApplicationIdle` للتحقق أثناء حلقة `ShowDialog()`
المتداخلة نفسها، واستضافة `SourcesView` داخل `Window` حقيقية بـ`Show()`/`UpdateLayout()`). يغطي:
غياب التراكب من شجرة العرض؛ فتح النافذة عبر `AddNewCommand` (مصدر عادي) وعبر `AddNewNeutronCommand`
(مصدر نيتروني — **كلا النوعين داخل نفس النافذة**)؛ وإغلاق يدوي عبر ✕/Alt+F4 **من كل خطوة من خطوات
المعالج الثلاث** عبر `[Theory]`/`[InlineData(1,2,3)]` مع التقدّم بأمر `NextStepCommand` الحقيقي؛
وإعادة الفتح بعد الإغلاق اليدوي مع بدء المعالج من الخطوة 1؛ إضافة إلى السيناريو المُرحَّل أعلاه.

**تحقُّق TDD إلزامي:** بإرجاع `SourcesView.xaml`/`.xaml.cs` إلى `main` عبر `git checkout HEAD --` مع
إبقاء النافذة الجديدة وملف الاختبار (أي بحيث يبقى المشروع قابلاً للبناء ويُعزَل تغيير السلوك وحده بدل
الاكتفاء بفشل بناء `CS0246`) فشلت الاختبارات فعلاً (`Failed: 1, Passed: 0`، توقّف التشغيل)، ونجحت
جميعها بعد الاستعادة (`Failed: 0, Passed: 7`).

**النتائج:** 1170 اختباراً (Debug) و1168 (Release) محلياً، صفر فشل وصفر تجاوز في كلتيهما — أعلى من
العتبة المطلوبة (1161/1163). فارق الاختبارين بين Debug وRelease **بنيوي سابق الوجود لا علاقة له بهذه
الجولة**، ويطابق ما سجّلته الجولة 146 (1160/1158). التنويه مهم لأن `tests.yml` يشغّل الاختبارات
بـ`--configuration Release` حصراً، فرقم CI المتوقَّع هو 1168 لا 1170.
بناء `Debug` و`Release`: صفر أخطاء، خمسة تحذيرات سابقة الوجود فقط (`LoginWindow.xaml.cs` سطرا 104
و199 مكرَّرة عبر مسارَي csproj، و`ViewInstantiationTests.cs`)، لا تحذير جديد من الملفات المضافة.
لا ترحيلات EF ولا تغيير مخطط. لم تُلمس `LoginWindow`/`LoginView`/`SplashWindow`. أعاد
`change-verifier` إنتاج كل هذه الأرقام مستقلاً وأصدر PASS على البنود السبعة.

**نتيجة CI (التشغيل `34423987987` على الكوميت `e296a95`):** نجاح (`success`) خلال 3م40ث —
`Total tests: 1168`، صفر فشل، صفر أخطاء بناء، وخمسة تحذيرات مطابقة تماماً للتحذيرات السابقة الوجود
أعلاه بلا أي تحذير جديد. **CodeRabbit لم يراجع** لأن الـPR في وضع Draft (سلوك مقصود من إعداداته:
`reviews.auto_review.drafts` غير مفعّل) — لا توجد ملاحظات CodeRabbit للتوفيق بشأنها في هذه الجولة.

**انحراف عملية موثَّق:** أُضيف تحديث ملفَّي التوثيق في كوميت لاحق منفصل على نفس الفرع/PR بدل الكوميت
نفسه، لأن الكوميت الأول كان قد دُفع بالفعل، وتعديله (`--amend`) كان سيستلزم `force-push` وهو إجراء
مدمِّر يتطلب إذناً صريحاً. السبب الجذري: اتُّخذ قرار عدم تحديث التوثيق قياساً على الجولة 147 وحدها
(التي لم تُحدِّثه)، بينما الجولات 144/145/146 حدَّثته جميعاً — أي أن الجولة 147 هي الاستثناء لا القاعدة.
(الجولة 147 نفسها لا تزال غير مُوثَّقة في هذين الملفين؛ لم تُضَف رجعياً هنا لأنها خارج نطاق هذه الجولة.)

---

## 18. الجولة 154 — إدراج المصادر النيترونية ضمن بطاقة «عدد المصادر» في لوحة القيادة

**عيب سابق الوجود أُغلق في هذه الجولة:** بطاقة «عدد المصادر» الأولى في `DashboardView.xaml`
(`TotalSources`, يُحسَب في `DashboardViewModel.LoadDataAsync()`) كانت تُبنى حصراً من
`_sourceService.GetAllSources()` — المصادر العادية فقط. `INeutronSourceService` لم يكن مُشاراً إليه
إطلاقاً في `DashboardViewModel.cs`، فكل مصدر نيتروني نشط كان غائباً بنيوياً عن هذا الرقم رغم ظهوره
الصحيح في بطاقة `NeutronSourcesCount` المنفصلة بشاشة `SourcesView`.

**القرار المعماري (مُعتمَد مسبقاً من إدريس، نُفِّذ كما هو):** `TotalSources` نفسها أصبحت **الرقم
المُجمَّع** (عادي + نيتروني نشط) بلا بطاقة جديدة منفصلة — تحقَّق أولاً بالبحث الشامل عن كل استخدامات
`.TotalSources` المربوطة بـ`DashboardViewModel` أن `DashboardView.xaml` السطر 316 هو الرابط
الوحيد، فلا شيء آخر يعتمد على أن معناها «عادي فقط». أُضيفت خاصية نصية مُلاحَظة جديدة
`SourcesBreakdownText` (`ObservableProperty`) تُحسَب في `LoadDataAsync()` بعد معرفة كلا العددين، عبر
مفتاح ترجمة قالب جديد `LabelSourcesBreakdown` (`"{0} عادي + {1} نيتروني"` عربياً،
`"{0} regular + {1} neutron"` إنجليزياً) و`string.Format` (لا `GetFormat` مباشرة، كي لا يُعاد اسم
المفتاح كارتداد غير آمن). أُضيف صف ثالث (`RowDefinition Height="Auto"`) أسفل الرقم الكبير في البطاقة
الأولى بـ`DashboardView.xaml` يعرض هذا النص، بنفس نمط النصوص الفرعية القائمة أسفل عناوين المخططات في
نفس الملف (`FontSize="11"`, `Foreground="{DynamicResource TextSecondary}"`,
`HorizontalAlignment="Center"`, `Margin="0,2,0,0"` — مطابق حرفياً لـ`HistogramSubtitle`/
`ChartSourcesByIsotopeSubtitle`/`ChartSourcesByLocationSubtitle`).

**التنفيذ:** حقن اختياري جديد `INeutronSourceService? neutronSourceService = null` أُضيف كآخر وسيط في
مُنشئ `DashboardViewModel` (لا وسيط إلزامي جديد — يحافظ على توافق ثلاثة ملفات اختبار قائمة تبني
الكائن بالوسطاء الستة الإلزاميين فقط)، بنفس نمط الارتداد القائم لـ`alertService`/`globalSearchService`
(`App.ServiceProvider?.GetService(typeof(INeutronSourceService)) as INeutronSourceService`).
`LoadDataAsync()` يستدعي `_neutronSourceService?.GetAll() ?? new List<NeutronSource>()` — نفس الدالة
والنمط الآمن من القيمة الفارغة المُستعمَلين فعلياً في `SourcesViewModel.cs` لحساب `NeutronSourcesCount`
(وليس `GetTotalCount()` التي لم تُستعمَل هنا لأن دلالتها الدقيقة مقارنة بـ`GetAll()` غير مؤكَّدة).
`TotalSources = sources.Count + neutronSources.Count`. لا مساس بـ`UpdateTotalActivityItems()` ولا أي
من مخطط Bq/Ci/Histogram/منحنى التحلل/توزيع النظائر — خارج النطاق كما نصّ العقد صراحة، ولا يوجد نسبة
تغيير («عن اليوم السابق») لهذه البطاقة أصلاً فلا شيء يلزم إبقاؤه متسقاً هناك.

**مواضع أخرى لمفهوم «عدد المصادر» عُثر عليها ولم تُمَس عمداً (خارج نطاق هذه الجولة):**
`SourceService.GetTotalSourcesCount()`، منطق تصدير `SettingsViewModel`، و`LocationDetailsViewModel.
TotalSourcesCount` — لكل منها استهلاك مختلف عن بطاقة اللوحة، ولم يطلب العقد تغييرها.

**الاختبار:** اختبار جديد `DashboardViewModel_TotalSources_CombinesRegularAndNeutronCounts` في
`DashboardLogicTests.cs` يُمثِّل خدمتي `ISourceService`/`INeutronSourceService` بعددين مختلفين (28
عادياً، 4 نيترونياً)، يبني `DashboardViewModel` بحقن `neutronSourceService` صراحة، يستدعي
`LoadDataAsync()`، ويؤكد `TotalSources == 32` واحتواء `SourcesBreakdownText` على كلا الرقمين
كنصين فرعيين. الكائن يُتخلَّص منه (`Dispose()`) في `finally` لأن `DashboardViewModel` ينفّذ
`IDisposable` ويستعمل `WeakReferenceMessenger`.

**النتائج:** 1188 اختباراً (Debug) و1186 (Release) محلياً — بصفر فشل وصفر تجاوز في كلتيهما (+1 اختبار
جديد بهذه الجولة في كل تشغيل عن قاعدة ما قبلها). فارق الاختبارين بين Debug/Release بنيوي سابق الوجود
لا علاقة له بهذه الجولة (مطابق لما سجّلته الجولات 146/148). بناء `Debug`/`Release`: صفر أخطاء، نفس
خمس تحذيرات `CS8604` سابقة الوجود فقط (`LoginWindow.xaml.cs` سطرا 104 و199 عبر مساري csproj،
و`ViewInstantiationTests.cs`)، لا تحذير جديد من الملفات المعدَّلة. لا ترحيل EF ولا تغيير مخطط قاعدة
بيانات في هذه الجولة.

## 19. الجولة 153 — توحيد تصميم `AlertDialog` على نمط بطاقة تسجيل الدخول

**النطاق:** إعادة تصميم بصري بحت لـ`Sources-System-Project/Views/AlertDialog.xaml` (+ تعديل طفيف مرافق
في `AlertDialog.xaml.cs`) — النافذة المشتركة الوحيدة التي يُنشئها `DialogHelper.cs` لكل رسائل النظام
(Info/Warning/Error/Confirmation/InfoWithExtraOption، نحو 184 موضع استدعاء). استُبعد صراحةً من هذا
النطاق بقرار مسبق من قائد المشروع: `PasswordPromptDialog.xaml[.cs]` (نافذة تأكيد كلمة مرور المدير
المستقلة عن `DialogHelper`) و`MessageBanner` المضمَّن في خمس شاشات (SourcesView, UsersView,
RadioisotopesView, IsotopeLibraryView, AlertsView). لم تُمَس `LoginWindow`/`LoginView`/`SplashWindow` —
استُخدمت `LoginWindow.xaml` كمرجع بصري للقراءة فقط.

**التغيير البصري:** استُبدل الرأس المتدرّج اللون (`LinearGradientBrush` أفقي بلون `PrimaryColor`) بشارة
أيقونة دائرية مركزية (`IconBadge`, 64×64, `CornerRadius="32"`) فوق عنوان مُوسَّط، بنمط الشعارات الدائرية
في بطاقة تسجيل الدخول، مع زر إغلاق صغير أعلى يمين البطاقة بدل زر الإغلاق المدمج بالرأس الملوَّن سابقاً.
ذيل الأزرار فُصل عن خلفية `SidebarBackground` الداكنة السابقة إلى خلفية شفافة تتبع لون البطاقة نفسها مع
خط فاصل رفيع (`BorderColor`) بدلاً منها، وأُزيلت تجاوزات `Foreground="White"`/`BorderBrush="#55FFFFFF"`
المُثبَّتة يدوياً على `NoButton`/`CancelButton` (كانت ضرورية فقط للتباين فوق الخلفية الداكنة القديمة)
لصالح الألوان الافتراضية الديناميكية لنمط `SecondaryButton` — هذا يُزيل تلوينات ثابتة بدل إضافتها، ولا
يُدخل أي لون Hardcoded جديد. كل الموارد المستخدمة ديناميكية موجودة أصلاً (`CardBackground`,
`TextPrimary`, `TextSecondary`, `BorderColor`, `PrimaryBrush`, `WarningBrush`, `DangerBrush`).

**قرار الشعار مقابل الأيقونة (انحراف مُبرَّر عن حرفية اقتراح العقد بشعار `sources_logo.png`):** استُخدمت
أيقونة `MaterialDesign:PackIcon` بلون شارة دائرية يتغيّر حسب نوع الرسالة (Info/Question=`PrimaryBrush`,
Warning=`WarningBrush`, Error=`DangerBrush`) بدل الشعار الكامل. السبب الهندسي: (1) الشعار ثابت الشكل
لا يميّز بين أنواع الرسائل الأربعة كما تفعل الأيقونة (نفس آلية `AlertIcon.Kind` القائمة أصلاً)؛
(2) استخدام شعار مؤسَّسي كامل لكل تنبيه/تحذير/خطأ يومي (184 موضع استدعاء) مبالغ فيه بصرياً مقارنة
بشاشة الدخول الرسمية التي تُعرض مرة واحدة فقط؛ (3) تفادي الاعتماد على تحميل ملف صورة عبر
`pack://siteoforigin` وقت الإنشاء لكل حوار (نمط `LoginWindow` غير المُختبَر سابقاً في هذا المسار)
يُبقي `AlertDialog` خالياً من أي اعتماد على نظام الملفات في السيناريو الشائع، متّسقاً مع اعتماد الاختبار
`ExitWarningDialog_RendersCorrectly_WithPendingChangesMessage` الذي يُصيِّر الحوار دون صورة. العقد نفسه
أجاز صراحة استخدام الحكم الهندسي هنا مع توضيح السبب.

**تعديل مرافق في `AlertDialog.xaml.cs` (بصري فقط، لا مساس بمنطق الأزرار):** نُقل تلوين النوع من
`AlertIcon.Foreground` (سابقاً لون أحمر فقط لحالة الخطأ فوق أيقونة بيضاء دائماً) إلى
`IconBadge.Background` لكل الأنواع الأربعة (كان يُضبط للخطأ فقط سابقاً) — امتداد مباشر لنفس مفتاح
`switch (type)` القائم أصلاً وليس منطقاً جديداً. أسماء العناصر المرتبطة بالكود
(`AlertIcon`, `TitleText`, `MessageText`, `ImageContainer`, `SourceImage`, `OkButton`, `ExtraButton`,
`YesButton`, `NoButton`, `CancelButton`) بقيت كما هي حرفياً؛ أُضيف `x:Name="IconBadge"` جديد فقط.
معالجات `*_Click` (`CloseButton_Click`, `OkButton_Click`, `ExtraButton_Click`, `YesButton_Click`,
`NoButton_Click`, `CancelButton_Click`) لم تُمَس إطلاقاً — نفس ضبط `Result` ونفس استدعاء `Close()`.

**قرار `FlowDirection` (لا تغيير، مُوثَّق كقرار مدروس لا كسهو):** فُحص نمط `PasswordPromptDialog.xaml`
الذي يضبط `FlowDirection="{DynamicResource CurrentFlowDirection}"` على مستوى `Window`. تبيَّن بالبحث
الشامل في المستودع أن المفتاح `CurrentFlowDirection` **غير مُعرَّف في أي قاموس موارد ولا يُضبط برمجياً
في أي مكان** — أي أن هذا الربط في `PasswordPromptDialog` خامل فعلياً ويرتدّ صمتاً للسلوك الافتراضي
(`LeftToRight`). نسخ هذا النمط الخامل إلى `AlertDialog.xaml` لن يُغيّر أي سلوك حقيقي وقد يُوهم بدعم RTL
ديناميكي غير موجود فعلياً؛ لذا **لم يُضَف**. أُبقي `AlertDialog` بلا `FlowDirection` على مستوى `Window`
(كما كان)، مع بقاء `MessageText.FlowDirection="RightToLeft"` الصريح كما هو — وهو ما أثبته اختبار
`ExitWarningDialog_RendersCorrectly_WithPendingChangesMessage` القائم أصلاً أنه يعمل بشكل صحيح.

**السحب (Drag) وEscape:** لم يُضَف أي معالج سحب جديد (`MouseLeftButtonDown`) لم يكن موجوداً سلفاً —
خارج نطاق "تصميمي بصري فقط" كما نصّ العقد. سلوك `CancelButton`/`IsCancel="True"` بقي كما هو حرفياً (زر
مخفي افتراضياً إلا مع `showCancel=true`، فلا يُغلق أي حوار بـEscape لم يكن يُغلَق به سابقاً).

**اختبار جديد إلزامي (لم تُختبَر أزرار Yes/No داخل `AlertDialog` من قبل قط):** أُضيف اختباران في
`Sources.Tests/ViewInstantiationTests.cs`:
`AlertDialog_QuestionMode_YesButtonClick_SetsResultToYes` و
`AlertDialog_QuestionMode_NoButtonClick_SetsResultToNo` — يُنشئان `AlertDialog` بـ`isQuestion: true`،
يُحدِّدان الزر عبر `dialog.FindName("YesButton"/"NoButton")` (نفس أسلوب `FindName` المستعمل فعلياً في
اختبار `DashboardView` القائم)، ثم يُثيران `Button.ClickEvent` برمجياً عبر
`RaiseEvent(new RoutedEventArgs(Button.ClickEvent))` (النمط القياسي لمحاكاة نقرة زر في اختبار WPF
Unit)، ويؤكِّدان `dialog.Result == AlertDialog.AlertResult.Yes`/`No` بعد ذلك — يغلقان الحوار عبر نفس
مسار `Close()` القائم أصلاً في معالج النقر (نمط `.Close()` على نافذة لم تُستدعَ لها `Show()` مُستعمَل
فعلياً في عشرات الاختبارات القائمة، مثل `SourceFormWindowTests.cs`/`UserFormWindowTests.cs`). كلا
الاختبارين يُنفَّذ عبر `RunInSta` كالاختبار القائم `ExitWarningDialog_RendersCorrectly...`.

**النتائج:** Debug 1190/1190 نجاح (0 فشل/0 تجاوز، +2 عن قاعدة الجولة 154 بسبب الاختبارين الجديدين).
Release 1188/1188 نجاح (0 فشل/0 تجاوز) — فارق الاختبارين بين Debug/Release بنيوي سابق الوجود
(`Sources.Tests/TestDataGeneratorTests.cs` مُقيَّد بالكامل بـ`#if DEBUG`)، تحقَّق منه القائد مباشرة عبر
`git stash`/إعادة البناء ولا علاقة له بهذه الجولة. بناء `Debug`/`Release`: صفر أخطاء، نفس خمس تحذيرات
`CS8604` سابقة الوجود بالضبط (تحقَّق منها القائد بمقارنة البناء قبل وبعد التعديل عبر `git stash`) —
`LoginWindow.xaml.cs` سطرا 104 و199 (عبر مساري csproj)، و`ViewInstantiationTests.cs` (اختبار
`ExitWarningDialog` القائم أصلاً، لا علاقة له بالاختبارين الجديدين). لا ترحيل EF ولا تغيير مخطط قاعدة
بيانات في هذه الجولة. `DialogHelper.cs` لم يُمَس — توقيعاته ومنطقه كما هما تماماً.

## 20. الجولة 155 — توحيد تصميم `PasswordPromptDialog` على نمط بطاقة تسجيل الدخول

**النطاق:** إعادة تصميم بصري بحت لـ`Sources-System-Project/Views/PasswordPromptDialog.xaml` فقط. هذا
هو الاستثناء الذي أجّلته الجولة 153 صراحةً (البند 19 أعلاه): تلك الجولة وحّدت فقط آلية `AlertDialog`
المشتركة التي يُنشئها `DialogHelper.cs`، واستبعدت `PasswordPromptDialog` بقرار مسبق لأنها نافذة WPF
مستقلة تماماً عن `DialogHelper` (لا تُفتح عبره، بل عبر `PasswordPromptDialog.RequestAdminAccess`
المستدعاة مباشرة من الشيفرة). هذه الجولة توحّد **المظهر البصري فقط** لتطابق نمط بطاقة تسجيل الدخول
(الشارة الدائرية المركزية + العنوان + ذيل بخط فاصل رفيع بدل الرأس المتدرّج)، دون أي تغيير على آلية
النافذة المنفصلة نفسها — تبقى `PasswordPromptDialog` نافذة WPF قائمة بذاتها تُفتح عبر `ShowDialog()`،
لا حواراً مُمرَّراً عبر `DialogHelper`.

**التغيير البصري:** أُزيل الرأس المتدرّج (`HeaderBorder` بخلفية `LinearGradientBrush` من
`PrimaryLightColor` إلى `PrimaryColor`) وزر الإغلاق الأبيض المدمج بداخله، واستُبدلا بنفس رأس
`AlertDialog`/بطاقة الدخول: `Grid` علوي بزر إغلاق صغير أعلى اليمين (`MaterialDesignIconButton`,
أيقونة `Close`, `Foreground="{DynamicResource TextSecondary}"`) و`StackPanel` مُوسَّط يضم شارة دائرية
(`IconBadge`, 64×64, `CornerRadius="32"`, `Background="{DynamicResource PrimaryBrush}"`) بداخلها أيقونة
`ShieldLockOutline` بيضاء 32×32 (نفس الأيقونة التي كانت مستخدمة سابقاً في `SecurityIcon` داخل الرأس
المتدرّج، أُعيد استخدام نفس `x:Name="SecurityIcon"` والنوع لتفادي ازدواج بصري مع أيقونة `KeyVariant`
الموجودة أصلاً في حاوية حقل كلمة المرور)، يليها `TitleText` بخط `FontSize="18" FontWeight="Bold"`
مُوسَّط. أُضيف `BorderBrush="{DynamicResource BorderColor}" BorderThickness="1"` على `MainBorder`
(لم يكن موجوداً سابقاً، مطابقةً لـ`AlertDialog`). ذيل الأزرار: أُزيلت خلفية `SidebarBackground` الداكنة
وأُضيف بدلاً منها خط فاصل رفيع (`Border Height="1" Background="{DynamicResource BorderColor}"`) فوق
الأزرار مباشرة على خلفية البطاقة نفسها، وأُزيلت تجاوزات `Foreground="White"`/`BorderBrush="#55FFFFFF"`
المُثبَّتة يدوياً على `CancelButton` (كانت ضرورية فقط للتباين فوق الرأس الداكن القديم، غير لازمة الآن
فوق `CardBackground`). لا تغيير في هوامش/حشوة الجسم (`PromptText` وحاوية `TxtPassword`) بخلاف تعديل
طفيف لحشوة الحاوية الخارجية (`Padding="25,4,25,20"`) لمطابقة إيقاع تباعد الرأس الجديد.

**أسماء العناصر ومنطق الكود-خلف (بلا أي تغيير):** `TxtPassword`, `PromptText`, `TitleText`,
`ConfirmButton`, `CancelButton` بقيت كما هي حرفياً. `PasswordPromptDialog.xaml.cs` **فرق صفري تماماً**
(تحقَّق منه القائد عبر `git diff` بعد التعديل) — `ConfirmButton_Click`, `CancelButton_Click`,
`CloseButton_Click`, `TxtPassword_KeyDown`, `ValidateAdminPassword`, `RequestAdminAccess`,
`RequestAdminAccessAsync`, `Result`, `CustomPromptResult` كلها كما هي بالضبط.

**اختباران جديدان في `Sources.Tests/ViewInstantiationTests.cs`:**
1. `PasswordPromptDialog_CancelButtonClick_SetsResultToFalse` — يُثير `CancelButton.Click` برمجياً
   ويؤكِّد `dialog.Result == false`. بخلاف اختباري `AlertDialog` في الجولة 153 (اللذين يُغلقان الحوار عبر
   `.Close()` فقط دون `ShowDialog()`)، فإن `CancelButton_Click` في `PasswordPromptDialog` يضبط
   `DialogResult` أيضاً — وهذه الخاصية في WPF يُسمح بضبطها فقط بعد فتح النافذة عبر `ShowDialog()`. لذا
   استُخدم نمط `Dispatcher.CurrentDispatcher.BeginInvoke(..., DispatcherPriority.ApplicationIdle)`
   القائم فعلياً في `LocationsFormWindowTests.cs`/`SourceFormWindowTests.cs`/إلخ لجدولة النقر أثناء حلقة
   `ShowDialog()` المتداخلة نفسها، ثم التأكيد على `dialog.Result`/`dialog.DialogResult` بعد عودة
   `ShowDialog()`.
2. `PasswordPromptDialog_RequestAdminAccess_ConfirmPath_HonorsCustomPromptResultTrue` — **انحراف موثَّق
   عن نص العقد المفضَّل** (خيار الحقن المباشر لـ`ConfirmButton_Click`): `ConfirmButton_Click` يقرأ
   `IUserService` عبر `App.ServiceProvider` مباشرة (خاصية `static IServiceProvider ServiceProvider
   { get; private set; }` — `setter` خاص، لا يوجد أي نمط قائم في `Sources.Tests` لحقن هذه الخاصية من
   اختبار). لذا طبَّقنا البديل الذي أجازه العقد صراحة عند تعذُّر الحقن: استخدام خُطّاف الاختبار الرسمي
   `PasswordPromptDialog.CustomPromptResult` مع `RequestAdminAccess()` (نفس نمط
   `RequestAdminAccess_HonorsCustomPromptResult` القائم في `DeletionsAndAdminPromptTests.cs`) بدل
   استدعاء `ConfirmButton_Click` عبر `RaiseEvent` مباشرة. هذا يختبر مسار "منح الوصول = true" الكامل
   (`RequestAdminAccess` يُعيد `CustomPromptResult.Value` دون فتح أي نافذة فعلية في وضع الاختبار)
   لا نقرة الزر ذاتها على عنصر UI، وهو ما وثَّقه العقد كتنازل مقبول.

**النتائج:** Debug 1192/1192 نجاح (0 فشل/0 تجاوز، +2 عن قاعدة الجولة 153/154 بسبب الاختبارين الجديدين).
Release 1190/1190 نجاح (0 فشل/0 تجاوز) — فارق الاختبارين بين Debug/Release هو نفس الفارق البنيوي
سابق الوجود الموثَّق في البند 19 (`TestDataGeneratorTests.cs` مُقيَّد بـ`#if DEBUG`)، لا علاقة له بهذه
الجولة. بناء `Debug`/`Release` (بناء كامل غير تزايدي `--no-incremental` للتحقق): صفر أخطاء، نفس خمس
تحذيرات `CS8604` سابقة الوجود بالضبط (`LoginWindow.xaml.cs` سطرا 104 و199 عبر مساري csproj،
و`ViewInstantiationTests.cs` سطر 218 اختبار `ExitWarningDialog` القائم أصلاً) — لا تحذيرات جديدة. لا
ترحيل EF ولا تغيير مخطط قاعدة بيانات في هذه الجولة.

**الحالة المتبقية غير المُوحَّدة بصرياً:** آلية `MessageBanner` المضمَّنة في خمس شاشات (SourcesView,
UsersView, RadioisotopesView, IsotopeLibraryView, AlertsView) تبقى خارج نطاق التوحيد — وهي آلية مختلفة
جوهرياً (شريط رسائل مُضمَّن داخل الشاشة نفسها، لا نافذة منبثقة مستقلة)، فلا معنى لتطبيق نمط "بطاقة
نافذة" عليها. بعد هذه الجولة، آليتا الحوار المنبثق الوحيدتان في النظام (`AlertDialog` عبر `DialogHelper`
و`PasswordPromptDialog` المستقلة) موحَّدتان بصرياً بنفس نمط بطاقة تسجيل الدخول؛ `MessageBanner` يبقى
الاستثناء الوحيد المتبقي عمداً.

## 21. الجولة 156 (الدفعة الثانية من سلسلة ترجمة طبقة `ViewModels` المتبقية، موسَّعة) — ترجمة
`DashboardViewModel.cs`/`LeakTestsViewModel.cs`/`SettingsViewModel.cs`/`SourceDetailsViewModel.cs`/
`BorrowViewModel.cs`، وتصحيح تقدير `IsotopeLibraryService.cs`

**النطاق المتفق عليه أولاً:** أربعة ملفات (`DashboardViewModel.cs`, `LeakTestsViewModel.cs`,
`IsotopeLibraryService.cs`, `SettingsViewModel.cs`) بتقدير الجولة 150 التقريبي ~107 نصاً إجمالاً
(~28+~27+~26+~26). **وسَّع إدريس النطاق أثناء نفس الجولة** بملفين إضافيين من قائمة الـ50 ملفاً
المتبقية بعد الجولة 151 (`SourceDetailsViewModel.cs`, `BorrowViewModel.cs`) بعد التحقق المباشر من
استثناءاتهما (فلتر حالة الاستعارة، أسماء ملفات التصدير، `RequiredResetPhrase`، تطبيع `freq`).

**اكتشاف دقيق أظهر فارقاً كبيراً عن التقدير — تحقَّق منه القائد قبل التنفيذ لا بالاعتماد على عدّ آلي:**
العدد الفعلي القابل للترجمة في الملفات الأربعة الأصلية كان **26 نصاً فقط (32 مع نصوص `#if DEBUG`
المستبعدة)**، لا ~107. القائد أوقف التنفيذ وأرسل تقرير اكتشاف مفصَّل لإدريس (دون الحد الأدنى 75
المحدَّد صراحة في عقد الجولة)، ووافق إدريس على المتابعة بالعدد الفعلي المُدقَّق مع تفسير السبب:
1. **`IsotopeLibraryService.cs`: تصحيح كامل من ~26 إلى 0.** النصوص العربية الوحيدة في الملف
   (`arabicToSymbolMap`، الأسطر ~312-339) قاموس داخلي يربط أسماء عربية شائعة للعناصر (كوبالت، سيزيوم،
   يود...) برموزها لدعم البحث — مفتاح منطق بحث/تطبيع لا نص معروض، مطابق تماماً لفئة "مقارنة منطق أعمال
   ضد قيمة حرفية" المستثناة من الترجمة. تقدير الجولة 150 عدَّها على الأرجح نصوصاً عربية خام دون تمييز
   طبيعتها كمفاتيح قاموس داخلية.
2. **`DashboardViewModel.cs`/`SettingsViewModel.cs`: الغالبية العظمى من النصوص العربية الخام في هاتين
   الملفين كانت بالفعل مربوطة**، بالشكل القائم منذ الجولة 130: `TranslationHelper.GetString("Key") ??
   "النص العربي الأصلي"` (النص بعد `??` مجرد ارتداد آمن عند غياب المفتاح، لا نص حرفي غير مربوط). تقدير
   الجولة 150 على الأرجح عدَّ هذه الأنماط ضمن النصوص غير المُترجَمة.

**التنفيذ الفعلي (6 ملفات، بنفس نمط الجولات 130-151 تماماً — `TranslationHelper.GetString`/`GetFormat`
مع ارتداد عربي مطابق حرفياً):**
- `DashboardViewModel.cs` **(5 مواضع استخدام، 3 مفاتيح جديدة)**: كلمة "مصدر" الملحقة برقم في تلميحات
  الرسوم البيانية الثلاثة (Tooltip formatters، الأسطر ~831/1009/1140 — مفتاح واحد مشترك
  `TextSourceUnit` يُستهلَك داخل نفس استدعاء `ArabicReshaper.ReshapeAndReverse` القائم دون تغيير منطق
  التشكيل)، وتسميتا الخطورة `SeverityLabel` (سطرا 1798-1799: `LabelSeverityCritical`/
  `LabelSeverityWarning`، بارتداد يحافظ على تفرُّع `IsArabic` الأصلي بين النصين العربي/الإنجليزي
  الثابتين سابقاً إن غاب المفتاح). **مفتاح الجولة 154 `LabelSourcesBreakdown` لم يُمَسّ** — تحقَّق
  القائد أنه مربوط بشكل صحيح مسبقاً في كلا القاموسين.
- `LeakTestsViewModel.cs` **(24 موضع استخدام، 26 مفتاحاً جديداً + إعادة استخدام 5 مفاتيح قائمة
  `TitleSuccess`/`TitleConfirmDelete`/`LabelSourceCode`/`LabelNotes`/`TextUnspecified` دون تكرارها)**:
  أول ملف من هذه السلسلة لم تُمسّه أي جولة سابقة إطلاقاً — كل رسائله كانت تُمرَّر مباشرة لـ
  `DialogHelper.Show*` بلا أي تغليف. غطَّت الترجمة: نص حالة ترقيم الصفحات (`MsgPageStatus`،
  `GetFormat` بأربعة متغيرات)، عنواني النموذج المنبثق للإضافة/التعديل، أربع رسائل تحقق فشل + عناوينها،
  رسالتا نجاح الحفظ (أُعيد استخدام `TitleSuccess` القائم)، تأكيد الحذف ونجاحه، عشرة تسميات في نص
  تفاصيل السجل (`ViewRecordDetails`)، عنوان نافذة التفاصيل، عنوانا تقريري PDF/Excel، ورسالتا فشل
  التصدير. **قيد معماري موثَّق (يكرر نمط الجولتين 130/131):** حقل `record.ArabicResult` (خاصية
  `[NotMapped]` تُعيد نصاً عربياً دائماً بصرف النظر عن لغة الواجهة) بقي دون تغيير — غُلِّفت تسمية الحقل
  نفسها ("نتيجة الفحص:") فقط، لا القيمة المعروضة بجانبها.
- `SettingsViewModel.cs` **(موضع استخدام واحد فقط، مفتاح جديد واحد)**: فلتر ملفات حوار استعادة النسخة
  الاحتياطية (`FilterBackupFiles`، سطر ~344). **استثناءات موثَّقة بقرار إدريس، لم تُمَسّ إطلاقاً:**
  `RequiredResetPhrase` (سطر 88 — يُقارَن مباشرة بإدخال المستخدم لتأكيد إعادة الضبط، ترجمة عرضها تُكسر
  المطابقة)، تطبيع `if (freq == "يومي"/"أسبوعي"/"شهري")` (الأسطر 135-137 — تحويل قيمة تخزين قديمة لا
  نص معروض)، وكامل كتلة `#if DEBUG` (الأسطر ~569-622، توليد بيانات تجريبية — كود تطويري لا يصل
  لمستخدم Release).
- `SourceDetailsViewModel.cs` **(~19 موضع استخدام، 19 مفتاحاً جديداً + إعادة استخدام مفتاحي
  `AlertError`/`TitleWarning` القائمين لكل تكرارات عنواني "خطأ"/"تنبيه" دون تكرارهما)**: تسمية نوع
  المصدر (مختوم/غير مختوم)، ثلاث رسائل تحذير معدل الجرعة، ثلاث تسميات حالة مساهمة النظير في معدل
  الجرعة (بينها `LabelDoseRateNonContributingFormat` بمتغير واحد يحافظ على ارتداد الرمز الفيزيائي
  "α/β" كما هو دون ترجمته)، فلترا حوار إرفاق/تنزيل الشهادات (بينها مفتاح صيغة `FilterByExtensionFormat`
  للحالة المشروطة بامتداد الملف)، وسبع رسائل خطأ/تحذير في دوال إدارة الشهادات (إرفاق/فتح/تنزيل/حذف) لم
  تكن مُغلَّفة إطلاقاً رغم وجود مثيلات مجاورة مُغلَّفة مسبقاً في نفس الدوال منذ جولة سابقة — عولجت
  بنفس النمط لضمان الاتساق. مفتاح واحد مشترك `TextUnknown` ("غير معروف") لموضعي ارتداد اسم المستخدم
  الحالي عند إرفاق/حذف الشهادة.
- `BorrowViewModel.cs` **(~8 مواضع استخدام، 4 مفاتيح جديدة + إعادة استخدام مفتاحي `TextUnspecified`/
  `LabelLocation` القائمين)**: ارتداد "غير محدد" لاسم النظير/الموقع في ملخص المصدر المختار (ثلاثة
  مواضع، مفتاح مُعاد استخدامه)، تسمية "الموقع:" (أُعيد استخدام `LabelLocation` الموجود مسبقاً بنفس
  النص الحرفي)، رسالة تأكيد التسليم (`MsgConfirmDeliverSourceFormat`، بمتغيرين، تطلَّبت
  `xml:space="preserve"` على عنصر `system:String` في كلا القاموسين لأن `&#xA;` يُنهار افتراضياً لمسافة
  واحدة عند القراءة عبر XAML بلا هذه الخاصية — اكتُشف الخلل فعلياً باختبار فاشل قبل تصحيحه)، ورسالتا
  نجاح التصدير + رسالة فشل تصدير مشتركة (`MsgErrExportFailedFormat`، مُستخدَمة في مساري PDF وExcel).
  **استثناءات موثَّقة بقرار إدريس، لم تُمَسّ إطلاقاً:** قائمة فلتر حالة الاستعارة الكاملة (`"الكل"`,
  `"تم التسليم"`, `"تم الإرجاع"`, `"متأخر"`, `"قريبة الإرجاع"`) ومقارناتها المباشرة في منطق الفلترة
  وتحويل الحالة (الأسطر ~134/138/546/548/561-563 — ترجمة العرض فقط ستُكسر المطابقة، يحتاج تصميم
  Enum+Converter في جولة منفصلة لاحقة، **مخاطرة معمارية متبقية مُسجَّلة**)، وأسماء ملفي تصدير PDF/Excel
  بالعربية (سابقة معتمدة من جولات سابقة مدموجة).

**إجمالي المفاتيح الجديدة: 53 مفتاحاً** (3+26+1+19+4) أُضيفت لكلا القاموسين
`Strings.ar.xaml`/`Strings.en.xaml` بدون أي تعديل على مفتاح قائم مسبقاً (بما فيها مفاتيح الجولة 154).

**الاختبارات:** ملف جديد `Sources.Tests/Round156TranslationTests.cs` بخمسة اختبارات انحدارية (واحد
لكل ملف من الخمسة المُترجَمة فعلياً) تستخدم نفس آلية تبديل القاموس النشط المعتمدة في الجولات 130-151
(استبدال `Strings.ar.xaml` بـ`Strings.en.xaml` عبر Uri مطلق)، وتُثبت أن نفس مسار الكود المُستخدَم فعلياً
في كل ViewModel (نفس اسم المفتاح، نفس استدعاء `GetString`/`GetFormat`) يُرجع القيمة الإنجليزية الصحيحة
فعلياً لا الارتداد العربي — **انحراف موثَّق عن الصياغة الحرفية للعقد:** لاختبار `DashboardViewModel.cs`
تحديداً، استُخدم التحقق المباشر من مخرجات `TranslationHelper` بنفس اسم المفتاح المُستهلَك فعلياً في
الكود، بدل بناء كائن `DashboardViewModel` كامل عبر Mocks لإثارة حساب `SeverityLabel` (يتطلب تجهيز
بيانات اضمحلال/نظائر إضافية غير ضرورية لإثبات صحة الربط بالترجمة نفسها) — نفس درجة التحقق الفعلي
(قيمة القاموس الحقيقية، لا فحص وجود المفتاح فقط) بتكلفة أقل. اختباران قائمان أُعيد تشغيلهما صراحة
للتأكد من عدم كسرهما: `MessageBannerBindingTests.cs` (الجولة 152) و`DashboardLogicTests.cs` (الجولة
154) — كلاهما ناجح بالكامل. اختبارا تكامل القاموس في `TranslationKeysTests.cs`
(`AllKeys_MustMatchBetweenArabicAndEnglishDictionaries`, `AllKeysUsedInCode_MustExistInBothDictionaries`)
ناجحان تلقائياً مع كل المفاتيح الـ53 الجديدة (الاختباران يفحصان القاموسين وكل ملفات `.cs` آلياً، لم
يتطلبا أي تعديل).

**النتائج:** Debug 1197/1197 نجاح (0 فشل/0 تجاوز، +5 اختبارات جديدة عن قاعدة الجولة 155). Release
1195/1195 نجاح (0 فشل/0 تجاوز) — نفس الفارق البنيوي بين Debug/Release سابق الوجود
(`TestDataGeneratorTests.cs` مُقيَّد بـ`#if DEBUG`)، لا علاقة له بهذه الجولة. بناء `Debug`/`Release`:
صفر أخطاء، نفس خمس تحذيرات `CS8604` سابقة الوجود بالضبط — **تحذير جديد واحد ظهر أثناء التنفيذ
(`LeakTestsViewModel.cs:231`، `record.Source?.SourceCode` مُمرَّر كوسيط `params object[]` قابل
لـ`null`) وأُصلح فوراً بإضافة `?? string.Empty` قبل إعادة البناء** — لا تحذيرات جديدة متبقية في
الإصدار النهائي. لا ترحيل EF ولا تغيير مخطط قاعدة بيانات في هذه الجولة.

**انحراف عملية مُسجَّل (يكرر نمط الجولتين 128/151):** تعذَّر تشغيل `round-implementer` داخل نفس
worktree الفرع القائم — الوكيل الفرعي عزل نفسه تلقائياً في worktree منفصل خاص به
(`agent-<id>`, فرع `worktree-agent-<id>`) ورفضت أداته الخاصة تنفيذ أي أمر Git خارج ذلك المسار، رغم أن
العقد المُرسَل حدَّد صراحة مسار/فرع worktree الجولة الصحيحين. القائد نفَّذ الجولة بالكامل مباشرة من
جلسة القيادة نفسها (تحديد المفاتيح، التحرير، حل تعارض التشفير مع محارف عزل الاتجاه الثنائي
`⁦`/`⁩` في سلسلتين نصيتين عبر بديل بايثون عندما فشلت أداة `Edit` القياسية على تطابق حرفي
دقيق، اكتشاف وإصلاح خلل انهيار `&#xA;` في XAML، البناء والاختبار الكامل مرتين) بدل التفويض الكامل،
بنفس نمط التحقق المعتاد (فحص نطاق الملفات، بناء واختبار كامل مرتين).

**الحجم المتبقي من ب5 بعد هذه الجولة:** يصعب حساب رقم دقيق واحد لأن الملفين الموسَّعين
(`SourceDetailsViewModel.cs`, `BorrowViewModel.cs`) لم يكن لهما تقدير فردي مُسجَّل من الجولة 150 ضمن
جدول الـ383/50-ملفاً الذي خلَّفته الجولة 151 — فقط رقم إجمالي مجمَّع. بأخذ العدد الفعلي المُترجَم في
الملفات الستة كخصم مباشر: 383 (بعد الجولة 151) − 30 (`Dashboard`+`LeakTests`+`Settings`، فعلياً
مُترجَم) − 23 (`SourceDetailsViewModel.cs`، فعلياً) − 8 (`BorrowViewModel.cs`، فعلياً) = **322 نصاً
عربياً عبر 44 ملفاً متبقياً** (50 − 6 ملفات هذه الجولة، بينها `IsotopeLibraryService.cs` الذي تبيَّن
أنه لم يكن يحتاج أي عمل من الأساس). **تحذير موثَّق لإدريس:** بما أن نمط المبالغة في تقدير الجولة 150
(عدّ أنماط `GetString(...) ?? "..."` المربوطة مسبقاً، وقواميس منطق الأعمال الداخلية كنصوص UI) تكرَّر
في أكثر من ملف هذه الجولة، فمن المرجَّح أن الرقم 322/44 مبالَغ فيه أيضاً بدرجة مشابهة؛ يُنصَح بإعادة
تدقيق سريع (لا إعادة اكتشاف كاملة) لبقية الـ44 ملفاً قبل الجولة القادمة من هذه السلسلة، بدل الاعتماد
على أرقام الجولة 150 كما هي.

## 22. الجولة 157 — نظام التفعيل والنسخة التجريبية (ب9)

### القرار المعماري

عند أول تثبيت تعمل المنظومة في وضع تجريبي: كل الشاشات ظاهرة وقابلة للتصفح الكامل، لكن أي عملية كتابة
بيانات (إضافة/تعديل/حذف) تُمنَع في طبقة الخدمة وتُعرِض رسالة توضّح أن النسخة تجريبية. شريط علوي ثابت
(`DockPanel.Dock="Top"` في `MainWindow.xaml`، فوق شريط التنقل السفلي وقبل محتوى الشاشة الرئيسي مباشرة)
يظهر عبر كل شاشات القشرة الرئيسية طالما `MainViewModel.IsTrialMode == true`، والنقر عليه يفتح
`ActivationDialog` (بنفس القالب البصري لـ`PasswordPromptDialog` بعد الجولة 155). عند إدخال رقم تسلسلي
صحيح: تُفعَّل المنظومة فوراً بلا إعادة تشغيل (`IsTrialMode = false` مباشرة)، وتُحفَظ علامة التفعيل
محلياً عبر DPAPI (`ProtectedData`, `DataProtectionScope.LocalMachine`) في
`%ProgramData%\Sources\license.dat` (مسار جديد أُضيف إلى `DatabasePaths.cs`:
`LicenseDirectory`/`LicenseFilePath`/`EnsureLicenseDirectory()`، اتساقاً مع قاعدة "كل المسارات من
`DatabasePaths`"). التحقق عبر تجزئة SHA-256 مقابل قائمة ثابتة صغيرة من التجزئات المُضمَّنة في الكود
(`LicenseService._validHashes`) — لا رقم تسلسلي صريح في الكود المصدري، ولا ربط بمعرِّف جهاز.

**تنبيه صريح لإدريس:** الرقم التسلسلي المُضمَّن حالياً في `LicenseService.cs` هو رقم عنصر نائب
(placeholder) بقيمة `"SOURCES-2026-TRIAL-ACTIVATE"` فقط لأغراض هذه الجولة. **يجب استبدال/إضافة
التجزئة الحقيقية في `_validHashes` قبل أي إصدار للإنتاج** — لا تُعامَل هذه القيمة كنهائية.

### ثغرة تفويض قائمة أصلاً — اكتُشفت أثناء هذه الجولة، لم تُصلَح

فحص الكود الفعلي (لا افتراض) أظهر أن `SourceService`، `NeutronSourceService`، `NeutronSourceTypeService`،
`LocationService`، و`RadioisotopeService` كانت تستدعي `AuthorizationGuard.RequireEditor` فقط في دوال
`Delete`/`Restore` — دوال `Create`/`Update` في هذه الخدمات الخمس لم تكن محمية بأي فحص تفويض إطلاقاً قبل
هذه الجولة (ثغرة غير متعلقة بب9 لكنها تتقاطع معها). **بموجب النطاق الصارم لهذه الجولة، أُضيف فحص
`RequireActivated` فقط لهذه الدوال — لم تُضَف حماية `RequireEditor` الناقصة**، لأن ذلك يتجاوز نطاق
"فحص التفعيل" المُتَّفق عليه ويُعَدّ تغيير منطق أعمال إضافي. **هذا اكتشاف مفتوح يحتاج جولة منفصلة
مستقبلية** لإغلاقه بشكل صريح (إضافة `RequireEditor` لدوال `Create`/`Update` في الخدمات الخمس).

### قائمة الدوال الأربعين المحمية بـ`RequireActivated`

12 خدمة، 40 دالة كتابة. القائمة الكاملة موثَّقة في تقرير التنفيذ المرفق بهذه الجولة. ترتيب الفحص حيث
يتقاطع `RequireActivated` مع `RequireEditor`/`RequireAdmin` القائمين: **`RequireActivated` أولاً**
دائماً (رسالة الوضع التجريبي تسبق رسالة "لا تملك صلاحية")، تحقيقاً لمعيار القبول رقم 1 في العقد.

### قرارات النطاق (ماذا استُثني ولماذا)

- **`BackupService.CreateBackup`**: مسموح في الوضع التجريبي — النسخة ستكون فارغة أصلاً، لا ضرر منها.
- **`BackupService.RestoreBackup`**: محمي — يُدخِل بيانات فعلية من ملف خارجي، بالضبط ما يجب منعه.
- **`SystemResetService.ResetSystemAsync`**: خارج النطاق كلياً — أداة إدارية استثنائية لها فحص خاص بها
  (`RequiredResetPhrase`)، ولا معنى لتصفير قاعدة بيانات فارغة أصلاً.
- **`AlertService.GenerateAlerts`**, **`BorrowService.CheckAndUpdateOverdue`**,
  **`SourceService.UpdateAllCurrentActivities`/`UpdateCurrentActivity`**: استُثنيت لأنها إعادة حساب
  آلية لقيم مُشتَقة تعمل تلقائياً عند فتح كل شاشة (Dashboard/Alerts/Borrow) — حظرها كان سيُظهر رسالة
  رفض في كل تنقّل عادي بالوضع التجريبي، وهي ليست إدخال بيانات من المستخدم أصلاً.
- **`IsotopeImportService`**: تُقرأ من مسار مطوِّر مُثبَّت (`D:\tmp\LibParser\isotopes_data.json`) غير
  موجود على أي تثبيت فعلي — أداة تطوير غير قابلة للوصول في الإصدار الفعلي.
- **`TestDataGeneratorService`**: مُقيَّدة بالكامل بـ`#if DEBUG`، غير موجودة في بناء `Release`.
- **`AuditService.Log`/`LogWithChanges`**: آثار جانبية داخلية تُستدعى بعد أن يكون فحص التفويض/الترخيص
  الخاص بالمُستدعي قد نجح فعلاً (أو ضمن مسارات إعادة حساب آلية مُستثناة) — حظرها مباشرة كان سيُسبِّب
  ازدواج فحص أو، أسوأ، فقدان سجل تدقيق بصمت لهذه الجولة نفسها.

### مخاطر متبقية مُوثَّقة (لم تُصلَح، خارج نطاق هذه الجولة)

- **`SystemSettingsService.ResetToDefaults`** يستدعي داخلياً `SaveSettings` (المحمية الآن). لم يُتحقَّق
  من أن نتيجة `(false, message)` من `SaveSettings` تُعاد فعلياً للمُستدعي أم تُبتلَع بصمت — `ResetToDefaults`
  دالة `void` أصلاً، فأي رفض من الحارس سيُصبح بصمت بحكم التوقيع، وهو سلوك قائم مسبقاً لا علاقة له بهذه
  الجولة تحديداً.
- **`AutoBackupService`** يستدعي `BackupService.CreateBackup()` (مسموح) ثم `SystemSettingsService.SaveSetting()`
  (محمية الآن) لتسجيل وقت آخر نسخة احتياطية. في الوضع التجريبي سيُرفَض هذا الحفظ بصمت (لأن `SaveSetting`
  دالة `void`)، فقد تتكرر عملية `CreateBackup` أكثر من المطلوب أثناء الفترة التجريبية. غير ضار (قاعدة
  بيانات فارغة) لكن يستحق الرصد.
- عدة دوال `void` (`AlertService.MarkAsRead`/`DismissAlert`/`MarkAllAsRead`،
  `SystemSettingsService.SaveSetting`/`SaveSettings`) لا تملك قناة إعادة رسالة، فالرفض في الوضع التجريبي
  صامت من منظور المُستدعي المباشر (لا استثناء، لا رسالة) — الواجهة لا تُظهر تنبيهاً منفصلاً لهذه الحالات
  تحديداً، بعكس دوال `(bool, string)`.

### الاختبارات

جميع اختبارات الخدمات الاثنتي عشرة المُعدَّلة زُوِّدت بـ`FakeLicenseService` (`IsActivated = true`
افتراضياً، جديد ضمن `Sources.Tests/Fakes/`) للحفاظ على سلوكها القائم بلا تغيير. أُضيف `LicenseServiceTests.cs`
(تفعيل رقم صحيح/خاطئ، حالة الغياب الافتراضية، استمرارية الحفظ عبر DPAPI)، `LicenseGuardRejectionTests.cs`
(تأكيد الرفض الفعلي لكل خدمة من الاثنتي عشرة عند `IsActivated == false`)، و`MainViewModelActivationTests.cs`
(خاصية `IsTrialMode` وتنفيذ `OpenActivationCommand` عبر خطاف اختباري جديد
`MainViewModel.TestActivationSerialOverride` بنفس نمط `PasswordPromptDialog.CustomPromptResult`).

**النتائج:** Release 1220/1220 نجاح (0 فشل/0 تجاوز). بناء `Release`: صفر أخطاء، نفس خمس تحذيرات
`CS8604` سابقة الوجود بالضبط (`LoginWindow.xaml.cs` سطرا 104 و199 عبر مساري csproj،
و`ViewInstantiationTests.cs` سطر 218) — لا تحذيرات جديدة من هذه الجولة. لا ترحيل EF ولا تغيير
مخطط قاعدة بيانات في هذه الجولة.

### انحراف تنفيذي مُسجَّل

نفس نمط الجولات 128/143/151/156: تعذَّر تشغيل `round-implementer` داخل نفس worktree الفرع القائم
(`round-157-license-trial-mode` محجوز بواسطة worktree آخر) — الوكيل عُزل تلقائياً في worktree منفصل
(`agent-<id>`, فرع محلي `round157-impl` مبنيّ فوق نفس Commit الأساس) ونُفِّذ العمل بالكامل من هناك، مع
دفع الفرع المحلي إلى الفرع البعيد الصحيح `round-157-license-trial-mode` عبر مسار مرجعي صريح
(`git push origin round157-impl:round-157-license-trial-mode`) بدل الاعتماد على تطابق أسماء الفروع
محلياً وبعيداً.

## 23. الجولة 158 — توطين تصدير PDF/Excel وفق لغة الواجهة النشطة

### القرار المعماري

`ReportingService.cs` (10 أنواع تقارير × Excel/PDF = 20 دالة توليد عامة) كان يحتوي عناوين تقارير،
أسماء أوراق Excel، رؤوس أعمدة، وعناوين أقسام كسلاسل نصية عربية مباشرة في الكود، تظهر بالعربية دائماً
بصرف النظر عن لغة الواجهة. مصدر معرفة اللغة النشطة المعتمد هو `SettingsHelper.Language` (وليس
`Thread.CurrentUICulture`) — كل قرار "عربي أم إنجليزي" في `ReportingService.cs` يستخدم دالتين مساعدتين
جديدتين: `IsEnglish()` (`SettingsHelper.Language == "en"`) و`T(key, arFallback)` (تُطابق نمط الدوال
الخمس الموجودة مسبقاً `GetNoDataText()` وأخواتها: `TranslationHelper.GetString(key) ?? arFallback`).

كل عنوان/رأس عمود/اسم ورقة/عنوان قسم ثابت استُبدِل بـ`T("مفتاح جديد", "النص العربي الأصلي")`. أُضيف
86 مفتاحاً جديداً بادئتها `Rpt` إلى كلا القاموسين `Strings.ar.xaml` (القيمة = النص العربي الأصلي حرفياً
بلا تغيير) و`Strings.en.xaml` (ترجمة إنجليزية مهنية متسقة مع مصطلحات الجولات السابقة). الاتجاه أصبح
شرطياً: `worksheet.RightToLeft = !IsEnglish()` في Excel، و`if (IsEnglish()) page.ContentFromLeftToRight(); else page.ContentFromRightToLeft();`
في PDF — تم التحقق من وجود `ContentFromLeftToRight` فعلياً في تجميعة `QuestPDF.dll` الفعلية المستخدَمة
(الإصدار `2026.2.3` المُثبَّت في `Sources.csproj`) عبر فحص الرموز الثنائية للمكتبة مباشرة قبل الاستخدام.

`reportTitle` الممرَّر من الـViewModel في الدوال الأربع (`GenerateInventoryReport*`,
`GenerateLeakTestsReport*`, `GenerateFailedLeakTestsReport*`, `GenerateNeutronInventoryReport*`) لم
يتغيَّر إطلاقاً — يبقى كما مُرِّر بلا أي معالجة أو ترجمة، تحقيقاً للقيد الصارم في عقد الجولة. التوطين
اقتصر على العناوين/الرؤوس/أسماء الأوراق/عناوين الأقسام الثابتة فقط داخل هذه الدوال، وليس على
`reportTitle` نفسه ولا على الافتراضي الداخلي المستخدَم فقط حين يكون `reportTitle` فارغاً (وهو نص
داخلي للخدمة وليس قيمة ممرَّرة من الـViewModel).

### خارج النطاق عمداً (سلاسل قيم بيانات، وليست عناوين/رؤوساً)

قيم مُشتقة من بيانات معروضة داخل صفوف التقرير — مثل تسميات نوع الموقع (`مختبر`/`مستودع / مخزن`/
`مستشفى`/`عيادة`)، بديل "غير محدد" لموقع مفقود، وحالة القفل ("مقفل مؤقتاً"/"طبيعي"/"لم يسجل بعد") —
تُركت بلا تغيير عمداً، لأن عقد الجولة يحصر التوطين صراحةً في "عناوين، أسماء أعمدة، عناوين أقسام"، وهذه
قيم بيانات لكل صف وليست عناوين ثابتة. هذا قرار نطاق موثَّق، وليس إغفالاً.

### الاختبارات

`ReportingServiceLocalizationTests.cs` (جديد، 10 اختبارات — واحد لكل نوع تقرير Excel من الأنواع
العشرة): يولِّد كل تقرير مرتين (`SettingsHelper.Language = "ar"` ثم `"en"` مع تبديل قاموس الموارد
النشط فعلياً بنفس آلية الجولات 130-156)، ويتحقق أن اسم الورقة/رأس عمود معروف يختلف فعلياً بين الحالتين
ويطابق القيمة المتوقعة من كل قاموس تحديداً (وليس فقط غياب الاستثناء)، إضافة للتحقق من `RightToLeft`.
اللغة الأصلية تُعاد دائماً في `finally` لتفادي تسريب الحالة بين الاختبارات.

**النتائج:** Release 1230/1230 نجاح (0 فشل/0 تجاوز، 10 اختبارات جديدة ضمن الإجمالي). بناء `Release`:
صفر أخطاء، نفس خمس تحذيرات `CS8604` سابقة الوجود بالضبط — لا تحذيرات جديدة من هذه الجولة. لا ترحيل EF
ولا تغيير مخطط قاعدة بيانات في هذه الجولة.

## الجولة 160 — استكمال شاشة المساعدة (ب7): إضافة 3 مواضيع جديدة (اختبارات التسرب، المحذوفات، المصادر النيوترونية) إلى `HelpViewModel` مع مفاتيح توطين ثنائية كاملة و7 اختبارات جديدة في `HelpViewModelTests.cs` (جديد)؛ Debug 1246/1246 نجاح (0 فشل/0 تجاوز)، لا تحذيرات جديدة، لا ترحيل EF.

## الجولة 161 — سد ثغرة صلاحيات التحرير في الإنشاء والتعديل (ب2، تابع)

**المصدر:** تدقيق خارجي مستقل (Codex + Antigravity/Opus + Antigravity/Gemini) بلَّغ أن `AuthorizationGuard.RequireEditor`
غائب عن مسارات الإنشاء والتعديل في ست خدمات، ولا يُستدعى إلا في الحذف والاسترجاع. تأكَّد الأمر بفحص مباشر للكود
(`grep` يثبت غياب `RequireEditor` عن `CreateSource`/`UpdateSource` وما يقابلها) قبل أي تنفيذ — لم يُعتمد تقرير
التدقيق كسند وحيد.

**الثغرة:** دوال الإنشاء/التعديل كانت تتحقق فقط من `RequireActivated` (تفعيل الترخيص)، لا من `IsEditor`/صلاحية القسم
الفعلية للمستخدم الحالي. مستخدم للاطّلاع فقط (`IsEditor=false`) قادر على استدعاء الخدمة مباشرة (متجاوزاً إخفاء
الأزرار في الواجهة) لإنشاء أو تعديل مصادر، مصادر نيترونية، نظائر، مواقع، سجلات استعارة، وسجلات فحص تسرب — بما
يشمل تحويل نتيجة فحص "راسب" إلى "ناجح". `LeakTestService` لم يكن يحتوي على `RequireEditor` من قبل بالمرة، حتى
في الحذف.

**الإصلاح:** إضافة `AuthorizationGuard.RequireEditor(_userService.CurrentUser, "<section>")` مباشرة بعد
`RequireActivated` في 14 دالة عبر `SourceService` (`CreateSource`/`UpdateSource`)، `NeutronSourceService`
(`Create`/`Update`)، `RadioisotopeService` (`Create`/`Update`)، `LocationService` (`Create`/`Update`)،
`BorrowService` (`CreateRequest`/`MarkReturned`)، و`LeakTestService` (`AddRecord`/`UpdateRecord`/`DeleteRecord`
الثلاثة، إذ لم يكن أي منها محروساً). إعادة استخدام صريحة لرسائل الترجمة القائمة (`MsgErrNotLoggedIn`،
`MsgErrReadOnlyUser`، `MsgErrNoSectionPermission`) بلا أي مفتاح جديد. 6 اختبارات جديدة في
`AuthorizationEnforcementTests.cs` (واحد لكل خدمة، يغطي: مستخدم غير مسجَّل، محرِّر بصلاحية قسم خاطئة، محرِّر
مخوَّل ينجح فعلياً).

**⚠ بند مفتوح رئيسي يتطلب قرار القائد (لم يُصحَّح بصمت):** كشف تطبيق الحارس أن 42 حالة اختبار (39 دالة اختبار) في
سبعة ملفات (`BorrowServiceTests.cs`، `LeakTestServiceTests.cs`، `AddedByUnificationTests.cs`،
`LocationServiceTests.cs`، `RadioisotopeServiceTests.cs`، `NonFinitePersistenceGuardTests.cs`،
`NeutronCalibrationInputTests.cs`) كانت تبني الخدمات بمستخدم اختباري بلا `IsEditor`/`Permissions` صحيحة أو بـ
`CurrentUser = null`، ثم تتوقع نجاح العملية — سيناريو كان الحارس الغائب يسمح به صمتاً. بعد مراجعة القائد لكل
الملفات السبعة وموافقة إدريس الصريحة، صُحِّحت التجهيزات في تسع حالات (تصنيف "خلل تجهيز" — إضافة `Permissions`
الصحيحة دون تغيير نية الاختبار الأصلية)، وأُعيدت كتابة ثلاث حالات (تصنيف "تصحيح سلوكي مقصود" — الإنشاء بمستخدم
`null` يجب أن يُرفض الآن، لا أن ينجح بـ `AddedBy=null`؛ الحالة الثالثة `RadioisotopeServiceTests.Create_WhenUserIsNull_...`
لم تُذكر صراحة بالاسم في قائمة الفئة الثانية الأصلية للقائد لكن التوجيه استبعدها من تصحيح التجهيز، وسلوكها بعد
الإصلاح مطابق للحالتين الأخريين فأُعيدت تسميتها بالنمط نفسه). التفاصيل الكاملة في commit التصحيح المنفصل.

**النتائج:** Debug 1252/1252 نجاح (0 فشل/0 تجاوز؛ +6 اختبارات جديدة عن الجولة 160، و1246+6=1252 يطابق). بناء بصفر
أخطاء وصفر تحذيرات جديدة. لا ترحيل EF ولا تغيير مخطط قاعدة بيانات. لم تُلمس الملفات الخمسة المحظورة. Draft PR غير
مدموج بعد.

## الجولة 162 — إصلاح نشر مكتبة النظائر المرجعية على الأجهزة المنشورة

**المصدر:** تدقيق خارجي بلَّغ أن `IsotopeLibraryService.cs` يحتوي مسار جهاز مطوّر مُحارَقاً، وتأكَّد الأمر بفحص
مباشر للكود من القائد قبل أي تنفيذ.

**الثغرة:** الدوال الأربع `GetIndexJsonPath`/`GetIcrpJsonPath`/`GetReferencePdfPath`/`GetIcrpPdfPath` كانت تبني
مصفوفة `candidatePaths` من أربعة مرشَّحين، رابعهم مسار مطلق حرفي على جهاز المطوّر تحديداً (مثال:
`@"d:\Sources-System\Sources-System-Project\Resources\References\gamma_constants_index.json"`). وفي الوقت نفسه،
`Sources.csproj` لم يكن ينسخ أي شيء من `Resources\References\` إلى مخرجات البناء — فقط `Assets\**\*.*` — فعلى أي
جهاز مُنشَّب فعلياً (لا نسخة تطوير على القرص `D:`)، المرشَّح الأول (`AppDomain.CurrentDomain.BaseDirectory`) لا
يجد الملفات أبداً، والمرشَّحان الثاني والثالث (صعود نسبي `..\..\..\` نحو شجرة المصدر) يعملان فقط صدفة على نسخة
تطوير محلية. النتيجة: قائمة مكتبة النظائر فارغة وأزرار فتح PDF المرجعي تفشل صامتاً على كل جهاز مُثبَّت غير جهاز
التطوير.

**الإصلاح:** حذف المرشَّح الرابع (السطر المُحارَق) من كل دالة من الدوال الأربع في `IsotopeLibraryService.cs` بلا
مساس بمنطق البحث/الترتيب/التطبيع/التخزين المؤقَّت في بقية الملف. وإضافة `ItemGroup` جديدة في `Sources.csproj`
تنسخ **حصراً** الملفات الأربعة التي تستهلكها المنظومة فعلياً (`gamma_constants_index.json`،
`icrp107_decay_index.json`، `14724519.pdf`، `ANIB_38_3.pdf`) بـ `CopyToOutputDirectory=PreserveNewest`، دون
لمس `ItemGroup` الأصول (`Assets`) القائمة ودون تضمين ملفات مساعدة خاصة بالمطوّر الموجودة في المجلد نفسه
(`convert_icrp107.py`، `extract_gamma_constants.py`، `__pycache__`، `gamma_constants_extraction_issues.json`،
`ICRP107_AnnexA_Radionuclide_Database_REVIEWED.xlsx`) التي لا يجب أن تُشحَن للمستخدم النهائي.

**التحقق:** `dotnet build -c Release` على `Sources-System-Project` نجح، وتأكَّد فعلياً بفحص القرص أن الملفات
الأربعة (ولا شيء غيرها) ظهرت في
`Sources-System-Project\bin\Release\net8.0-windows\Resources\References\` بعد البناء.

**اختبار جديد:** `ReferencePaths_NeverContainDeveloperMachineHardcodedPath` في `IsotopeLibraryServiceTests.cs`
يتحقق أن مخرجات الدوال الأربع لا تساوي أياً من المسارات الأربعة المُحارَقة المحذوفة تحديداً (مطابقة حرفية دقيقة،
لا فحص احتواء نصي فضفاض) — لأن فحص "لا يحوي السلسلة `d:\Sources-System`" غير سليم على جهاز التطوير الفعلي لهذا
المشروع تحديداً: مجلد العمل الرسمي المُثبَّت في توثيق المشروع هو حرفياً `D:\Sources-System`، فأي مسار يُحلّ داخل
مجلد مخرجات البناء لهذا المستودع نفسه سيحوي هذه السلسلة الفرعية بصورة شرعية بلا علاقة بالعيب المُصلَح — وهو ما
أظهره تشغيل تجريبي أول للاختبار بصياغة الاحتواء الفضفاضة (فشل 4 من 4 رغم إصلاح الكود فعلياً). انحراف مسجَّل عن
نص العقد الحرفي للاختبار الجديد، لسلامة الفحص لا لإضعافه؛ الاختبار الأصلي `ReferencePdf_PathsResolveAndFilesExist`
لم يُعدَّل ونجح كما هو عبر آلية النسخ الجديدة بدل الاعتماد على الصدفة.

**النتائج:** Debug 1253/1253 نجاح (0 فشل/0 تجاوز؛ +1 اختبار جديد عن الجولة 161). بناء `Release` بصفر أخطاء
و4 تحذيرات `CS8604` مسبقة الوجود بالضبط في `LoginWindow.xaml.cs` (نفس التحذيرات المذكورة في الجولات السابقة،
مكرَّرة مرتين في مخرجات هذا الأمر تحديداً بسبب مشروع تجميع WPF المؤقت `wpftmp`، بلا علاقة بهذه الجولة). لا ترحيل
EF ولا تغيير مخطط قاعدة بيانات. لم تُلمس الملفات الخمسة المحظورة (`LoginWindow`/`LoginView`/`SplashWindow` غير
معنيّة أصلاً بنطاق هذه الجولة). Draft PR غير مدموج بعد.

## الجولة 163 — إصلاح عدم استجابة 12 نافذة/عرض لتبديل اللغة زمن التشغيل + إصلاح مورد CurrentFlowDirection المكسور

**العيب الأول (12 نافذة "صمّاء"):** 12 ملف XAML (`SourceFormWindow`، `RadioisotopeFormWindow`،
`LocationFormWindow`، `UserFormWindow`، `BorrowFormWindow`، `NeutronSourceTypesWindow`،
`SourceDetailsWindow`، `NeutronSourceDetailsWindow`، `IsotopeDetailsWindow`، `ScreensaverWindow`،
`BorrowView`، `LeakTestsView`) كانت تضبط `FlowDirection="RightToLeft"` كقيمة حرفية ثابتة على العنصر
الجذر (`Window`/`UserControl`)، فتبقى دائماً من اليمين لليسار حتى بعد تبديل اللغة إلى الإنجليزية عبر
`SettingsViewModel` → `App.ApplyLanguage("en")`، رغم أن باقي عناصر الواجهة (النصوص) تتحدث فعلياً.

**العيب الثاني (مورد `CurrentFlowDirection` مكسور):** `ActivationDialog.xaml` و`PasswordPromptDialog.xaml`
كانا يستخدمان بالفعل `FlowDirection="{DynamicResource CurrentFlowDirection}"` — لكن هذا المفتاح لم يكن
مُعرَّفاً في أي مكان في المشروع (تأكَّد بفحص شامل للمستودع)، فكان WPF يستخدم القيمة الافتراضية
(`LeftToRight`) بصمت بلا أي استثناء أو تحذير.

**الإصلاح (النسخة النهائية بعد تصحيح لاحق — انظر أدناه):** في `App.xaml.cs`، داخل
`ApplyLanguage(string cultureCode)`، ينفَّذ حساب `newFlowDirection` وضبط
`app.Resources["CurrentFlowDirection"] = newFlowDirection;` **بلا شرط**، قبل/خارج
`if (app.MainWindow != null)`؛ يبقى داخل هذا الشرط فقط ما يحتاج فعلياً `MainWindow` غير `null`
(`app.MainWindow.FlowDirection`، `app.MainWindow.Language`، وإعادة تعيين المحتوى). هذا الترتيب
ضروري لأن `ApplyLanguage(SettingsHelper.Language)` تُستدعى من `OnStartup` **قبل** إنشاء أي نافذة
(`MainWindow` لا يزال `null` حينها)، ولا تُستدعى مجدداً بعد تسجيل الدخول إلا إذا بدّل المستخدم اللغة
يدوياً من الإعدادات؛ فلو بقي ضبط المورد مشروطاً بوجود `MainWindow`، لبقي المورد غير مضبوط طوال
الجلسة الافتراضية (عربي، بلا تبديل لغة يدوي) — وهذا بالضبط الشكل الأول (الخاطئ) الذي نُفِّذ به هذا
الإصلاح قبل أن يكتشفه القائد بمراجعة الملف الكامل (`commit c5896e3`) ويصحَّح فوراً (`commit 2f56f2d`)
قبل أي دمج. وفي الملفات الاثني عشر، استُبدلت القيمة الحرفية `FlowDirection="RightToLeft"` على العنصر
الجذر فقط بـ `FlowDirection="{DynamicResource CurrentFlowDirection}"`، دون لمس أي
`FlowDirection="LeftToRight"` حرفي آخر على عناصر متداخلة (أرقام/تواريخ/نصوص إنجليزية تبقى من اليسار
لليمين عمداً في هذه الملفات).

**استُبعد عمداً من النطاق:** `Resources/Styles.xaml` (سطرا ~400 و~523 — إعدادات `FlowDirection` على
أنماط `DataGrid`/`SearchBox`، سلوك تخطيطي مقصود لا علاقة له باتجاه نص الواجهة العام) و
`Views/AlertDialog.xaml` (سطر ~100 — `FlowDirection="RightToLeft"` على `TextBlock` متداخل، ليس على جذر
النافذة). كلاهما خارج نطاق هذه الجولة تحديداً ولم يُفحَصا كعيوب، بل استُبعِدا بقرار صريح لتفادي توسعة
النطاق بلا عقد مصادَق عليه.

**اختبار جديد:** `FlowDirectionResourceTests.cs` (اختباران) يتحقق أن مورد `CurrentFlowDirection` يُضبَط
على `FlowDirection.RightToLeft` لـ"ar" و`FlowDirection.LeftToRight` لـ"en". لا يستدعي الاختبار
`App.ApplyLanguage` مباشرة (مثل بقية حزمة الاختبارات)، بل يحاكي فقط عبارة الضبط نفسها — لأن
`App.ApplyLanguage` تستخدم Uri نسبياً لتحميل قاموس النصوص لا يُحل إلا ضمن `Sources.exe` المُصرَّف
فعلياً، وكائن `Application` الذي ينشئه `WpfStaFixture` (وليس `Sources.App`) لا يستطيع حل هذا الـ Uri،
وهو قيد بنيوي موجود مسبقاً في حزمة الاختبارات (تأكَّد بتجربة الاستدعاء المباشر فعلياً: `IOException:
Cannot locate resource 'resources/strings.ar.xaml'`)، وليس عيباً استحدثته هذه الجولة.

**النتائج:** Debug 1255/1255 نجاح (0 فشل/0 تجاوز؛ +2 اختبار جديد عن الجولة 162، ثابت عبر كلا الكوميتين).
بناء `Release` بصفر أخطاء وتحذيرين مميَّزين `CS8604` مسبقي الوجود بالضبط في `LoginWindow.xaml.cs`
(السطرين 104 و199، بلا علاقة بهذه الجولة) — يظهران أحياناً مكرَّرين أربع مرات في ملخص بعض استدعاءات
`dotnet build` التي تبني `Sources-System-Project.csproj` مباشرة (لا الحل `Sources.sln`)، بسبب مشروع
تجميع WPF المؤقت `wpftmp` الذي يُكرِّر تمرير التحذيرين، وهو نفس السلوك الموثَّق في الجولة 162؛ عدد
التحذيرات المميَّزة الفعلي **اثنان لا أربعة**، مؤكَّد باختبار `change-verifier` المستقل. لا ترحيل
EF ولا تغيير مخطط قاعدة بيانات. لم تُلمس الملفات الخمسة المحظورة، ولا `ActivationDialog.xaml`/
`PasswordPromptDialog.xaml` (كانتا صحيحتين مسبقاً وستعملان تلقائياً بعد تعريف المورد). Draft PR #58
غير مدموج بعد.

**تصحيح لاحق (`commit 2f56f2d`، على نفس الفرع/الـPR، قبل أي دمج):** راجع القائد الملف الكامل
`App.xaml.cs` (لا الـdiff فقط) فاكتشف أن التنفيذ الأول (`commit c5896e3`) وضع سطر
`app.Resources["CurrentFlowDirection"] = newFlowDirection;` **داخل** `if (app.MainWindow != null)`
خطأً — وهو خلل حقيقي لا وصفي: `OnStartup` يستدعي `ApplyLanguage(SettingsHelper.Language)` قبل تعيين
`MainWindow` لأي نافذة إطلاقاً، ولا تُستدعى `ApplyLanguage` بعد ذلك أبداً في الجلسة الافتراضية (عربي
بلا تبديل لغة يدوي)؛ فكان المورد **يبقى غير مضبوط طوال الجلسة بأكملها**، فيرتد
`{DynamicResource CurrentFlowDirection}` إلى الافتراضي `LeftToRight` — ارتداد (regression) فعلي عن
`RightToLeft` الحرفي السابق في الاثنتي عشرة نافذة، بالإضافة إلى `ActivationDialog`/
`PasswordPromptDialog`. الإصلاح: نقل حساب `newFlowDirection` وضبط المورد إلى خارج/قبل الشرط (الوصف
أعلاه هو الحالة النهائية بعد هذا التصحيح). لم يتغيّر أي شيء آخر في `App.xaml.cs` ولا في أي ملف آخر
بهذا الكوميت. الاختباران الجديدان (`FlowDirectionResourceTests.cs`) لا يعتمدان على ترتيب الاستدعاء
فبقيا ناجحين بلا تعديل. أعاد `change-verifier` التحقق المستقل من: ترتيب الكود في `App.xaml.cs`،
الملفات الاثني عشر، الاختبار الجديد، نطاق `git diff --stat`، 1255/1255 اختباراً، والبناء — وأصدر PASS
على الجميع.

## الجولة 164 — نقل مجلد الشهادات إلى AppData + فرض تغيير كلمة مرور admin الافتراضية

جولة بجزأين مستقلَّين في الملفات نُفِّذا تسلسلياً في نفس العقد (لا تنفيذ متوازٍ)، عبر `round-implementer`
مع تحقُّق مستقل لاحق من القائد و`change-verifier` و`ci-monitor`.

### الجزء الأول: نقل مجلد الشهادات (نفس نمط عيب الجولة 162 بالضبط)

**الثغرة:** `App.xaml.cs` كان يبني مجلد الشهادات بجانب الملف التنفيذي
(`Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Certificates")`)، ونفس التعبير الحرفي كان القيمة
الافتراضية لـ`_certificatesFolder` في `BackupService.cs` و`SourceCertificateService.cs`. على أي جهاز
مُنشَّب فعلياً (عادة تحت `Program Files`)، هذا المسار يتطلب صلاحيات مدير للكتابة، فيفشل أي مستخدم عادي في
رفع شهادة جديدة أو حذفها فور التنصيب الحقيقي — عيب مطابق تماماً لعيب الجولة 162 من حيث الجذر (اعتماد على
`BaseDirectory` بدل مجلد بيانات مستخدم) والأثر (صمت الفشل على كل جهاز غير جهاز التطوير).

**الإصلاح:** استبدال التعبير الافتراضي في الملفات الثلاثة بـ`Path.Combine(DatabasePaths.AppDataDirectory,
"Certificates")` — نفس المسار المُستخدَم فعلاً لقاعدة البيانات نفسها (`%LocalAppData%\Sources`، موثَّق في
`DatabasePaths.cs` بأنه "لا يحتاج صلاحيات مدير"). معامل `customCertificatesFolder` الاختياري في كلا
الـconstructors لم يُمسّ (يبقى يخدم الاختبارات كما هو). كلاس جديد `Data/LegacyCertificatesImporter.cs`
يُطابق نمط `LegacyDatabaseImporter.cs` القائم أسلوبياً (استدعاء صريح واحد من `OnStartup`، فوراً بعد
`LegacyDatabaseImporter.ImportIfNeeded()`)، لكنه يختلف عنه جوهرياً في معالجة الفشل: **نسخ لا نقل** (المجلد
القديم يبقى كما هو، لا يُحذف مطلقاً)، ويُنسخ فقط إذا كان القديم موجوداً وبه ملفات والجديد فارغاً/غير موجود؛
وأي فشل — جزئي (ملف واحد) أو كلي — يُلتقَط ويُسجَّل تحذيراً عبر `LoggerService.LogWarning` **بلا رمي أي
استثناء يوقف الإقلاع**، بخلاف `LegacyDatabaseImporter` الذي يرمي استثناءً عمداً لأن فقدان القاعدة أمر حرج؛
فقدان شهادة واحدة عند الاستيراد ليس بنفس الخطورة ويجب ألا يمنع فتح البرنامج بالكامل.

**اختبار جديد:** `LegacyCertificatesImporterTests.cs` (10 اختبارات) يثبت: المسار الافتراضي الجديد لكلا
الخدمتين يساوي `DatabasePaths.AppDataDirectory\Certificates`؛ الاستيراد ينسخ الملفات فعلياً (مسطحة
ومتداخلة في مجلدات فرعية) عند وجودها في المسار القديم؛ لا نسخ إذا كان القديم غائباً أو فارغاً أو كان الجديد
ممتلئاً بالفعل (بلا استبدال)؛ المصدر القديم لا يُحذف أبداً؛ ملف مقفول لا يرمي استثناء بل يُسجَّل تحذيراً
فقط ويكمل البقية.

### الجزء الثاني: فرض تغيير كلمة مرور admin الافتراضية عند أول تسجيل دخول

**الثغرة:** `AppDbContext.cs` كان يزرع مستخدم `admin` افتراضياً بكلمة مرور حرفية `"admin"` عند أول تشغيل
بلا أي آلية لإجبار تغييرها — ثغرة أمنية حقيقية موثَّقة علناً في الكود المصدري نفسه، لا نظرية.

**الإصلاح:** حقل جديد `public bool MustChangePassword { get; set; } = false;` على `User`
(`Models/AllModels.cs`)، مع migration EF إضافية آمنة بلا فقد بيانات (`AddColumn<bool>` فقط، انظر نص
السكربت أدناه). أُعيد هيكلة منطق زرع/ترقية admin في `AppDbContext.cs`: admin جديد يُزرَع بـ
`MustChangePassword = true` دائماً؛ admin موجود يُفحَص أولاً هل كلمة مروره لا تزال حرفياً `"admin"` (عبر
`VerifyPassword` الموجود فعلاً، بما في ذلك مسار ترقية الهاش القديم SHA256 — إذا رُقِّي الهاش فكلمة المرور
الفعلية لا تزال "admin" حكماً)؛ إن كانت كذلك ولم يكن الحقل مضبوطاً مسبقاً، يُضبَط `true`؛ أما admin غيَّر
كلمة مروره فعلياً فلا يُلمَس حقله إطلاقاً (لا إجبار على تغيير ثانٍ لمن غيَّرها بالفعل). `ResetPassword` في
`UserService.cs` (الدالة المُخوَّلة فعلاً عبر `AuthorizationGuard.RequireAdmin`، بلا أي دالة تخويل جديدة)
يُصفِّر الحقل (`user.MustChangePassword = false;`) بعد نجاح أي إعادة تعيين — صحيح دلالياً لكل استخدامات
الدالة، لا لهذا السيناريو فقط. `MainViewModel.OnLoginSuccess()` يفتح حواراً مودالياً جديداً
`ForceChangePasswordDialog` (قبل `NavigateTo("Dashboard")` مباشرة) عندما يكون `CurrentUser.MustChangePassword
== true`، مقيَّداً أيضاً بـ`!DialogHelper.IsTestMode` (نمط علم اختباري قائم فعلاً ومُستخدَم في نفس الملف
وملفات أخرى بالمشروع، لا آلية مُخترَعة جديدة). الحوار: بلا زر إلغاء، `WindowStyle="None"`، ومعالج
`Closing` يمنع الإغلاق (`e.Cancel = true`) قبل نجاح التغيير فعلياً — يسد X والـAlt+F4 معاً؛ يرفض تكرار
كلمة المرور الافتراضية `"admin"` صراحة (فحص جديد منفصل عن أي تحقق داخل `ResetPassword`) قبل استدعاء
`ResetPassword` نهائياً، إلى جانب رفض الحقل الفارغ وعدم التطابق بين الحقلين.

**بحث تأكيدي:** تأكَّد بفحص شامل للمستودع (`grep` على `PasswordHash =`) أن `UserService.ResetPassword` هو
المسار الوحيد الذي يغيّر كلمة مرور admin بعد الزرع الأولي (المسارات الأخرى: الزرع نفسه، وترقية SHA256
داخل `AppDbContext`، و`CreateUser` الخاص بإنشاء مستخدمين جدد لا تعديل موجودين) — فلا يوجد مسار بديل يمكن أن
يُبقي الحقل عالقاً `true` إلى الأبد.

**نص migration الكامل (`20260916065038_Round164_AddMustChangePassword.cs`، مُراجَع يدوياً):**
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<bool>(
        name: "MustChangePassword",
        table: "Users",
        type: "INTEGER",
        nullable: false,
        defaultValue: false);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropColumn(
        name: "MustChangePassword",
        table: "Users");
}
```
إضافة عمود واحدة فقط (`INTEGER` لـ`bool` في SQLite)، `nullable: false` مع `defaultValue: false` مطابقة
للقيمة الافتراضية على مستوى النموذج، بلا إعادة بناء جدول وبلا أي خطر فقد بيانات؛ `Down` يحذف العمود نفسه
فقط.

**اختبارات جديدة:** `AdminMustChangePasswordSeedTests.cs` (4 اختبارات: admin جديد يُزرَع بالعلم `true`،
إعادة الزرع لا تُلغي العلم القائم، admin غيَّر كلمة مروره فعلياً لا يُفرَض عليه العلم، وإعادة زرع admin لا
يزال بكلمة المرور الافتراضية يضبط العلم `true` رغم أنه لم يكن مضبوطاً من قبل) و`ForceChangePasswordDialogTests.cs`
(4 اختبارات: رفض "admin" الحرفية بلا استدعاء `ResetPassword`، رفض عدم التطابق بلا استدعاء `ResetPassword`،
نجاح كلمة مرور صالحة يستدعي `ResetPassword` ويُغلق بنجاح، وفشل `ResetPassword` يُظهر الخطأ ولا يُغلق)، إضافة
لاختبار جديد في `UserServiceTests.cs` (`ResetPassword_ClearsMustChangePasswordFlag`).

### النتائج

19 اختباراً جديداً بالإضافة الصرفة بلا أي حذف (10 في `LegacyCertificatesImporterTests.cs` + 4 في
`AdminMustChangePasswordSeedTests.cs` + 4 في `ForceChangePasswordDialogTests.cs` + 1 في `UserServiceTests.cs`،
مؤكَّد بـ`git diff --stat` على ملفات الاختبار وحدها بين `0613355` و`e431588`: أربعة ملفات، إضافات فقط، صفر
حذف). 1272/1272 اختباراً (Release)، صفر فشل، صفر تجاوز — الرقم مؤكَّد بتشغيل مستقل من القائد و`change-verifier`
كليهما على نفس الكوميت الفعلي `e431588`؛ فارق العدد الظاهري بين قاعدة الجولة 163 (1255 اختباراً بإعداد Debug)
و+19 اختباراً جديداً مقابل 1272 بإعداد Release (فرق 17 لا 19) متوافق مع الفجوة البنيوية الموثَّقة مسبقاً بين
عدَّاد Debug وRelease في هذه الحزمة (مثال: الجولتان 148/149 سجَّلتا فرق اختبارين ثابت بين الإعدادين لأسباب
بنيوية سابقة الوجود لا علاقة لها بهذه الجولة). بناء
`Release` بصفر أخطاء وخمسة تحذيرات `CS8604` سابقة الوجود بالضبط في `LoginWindow.xaml.cs` (سطرا 104 و199)
و`ViewInstantiationTests.cs` (سطر 218)، بلا أي علاقة بهذه الجولة (ملفات غير مُلمَسة في هذا الـdiff). CI
(GitHub Actions، التشغيل `35066435462`) نجح بالكامل. CodeRabbit لم يراجع بعد لأن الـPR في وضع Draft (سلوك
مقصود من إعداداته)، فلا ملاحظات للتوفيق بشأنها حتى الآن. لا تغيير في مخطط قاعدة البيانات عدا العمود
الإضافي الآمن أعلاه. لم تُلمس الملفات الخمسة المحظورة (`LoginWindow`/`LoginView`/`SplashWindow`) ولا
`AuthorizationGuard.RequireAdmin` ولا `FirstRunWizardWindow`. Draft PR #59 غير مدموج بعد.

### الانحرافات عن العقد الأصلي (موثَّقة صراحة، بلا تجميل)

1. **اسم الفرع:** الفرع الفعلي للـPR هو `worktree-agent-a3dfdc263d05442ce` لا
`round-164-certificates-path-and-forced-password-change` المتعاقَد عليه — لأن هذا الاسم الأخير كان
مُستخدَماً بالفعل في worktree القائد نفسه وقت تنفيذ الجولة عبر وكيل فرعي معزول في worktree منفصل، فتعذَّر
على الوكيل استخدام الاسم نفسه. الأساس والمحتوى والنطاق مطابقون تماماً للعقد؛ الفارق تسمية بحتة لا يؤثر على
الكود أو المراجعة.
2. **شرط `!DialogHelper.IsTestMode` إضافي:** أُضيف قبل عرض `ForceChangePasswordDialog` في
`MainViewModel.OnLoginSuccess()` — لم يرد نصاً في العقد الأصلي، أُضيف لتفادي فتح حوار مودالي فعلي (`ShowDialog`
حقيقي) أثناء تشغيل حزمة الاختبارات الآلية، وهو نمط علم اختباري قائم فعلاً ومُستخدَم مسبقاً في نفس الملف
(`MainViewModel.cs`) وملفات أخرى بالمشروع (`SourceNavigationHelper.cs`، `PasswordPromptDialog.xaml.cs`،
`LocationsViewModel.cs`) — تطبيق لنمط قائم لا اختراع آلية جديدة.
3. **فجوة تغطية اختبارية بسيطة:** لا يوجد اختبار مستقل مخصَّص لتفاعل مسار ترقية الهاش القديم SHA256 تحديداً
مع `MustChangePassword` (الحالة مغطاة جزئياً فقط: مسار الترقية نفسه مختبَر في مكان آخر بالحزمة، ومسار
"admin لا يزال بكلمة المرور الافتراضية" بعد الترقية يُنتِج نفس السلوك مباشرة كودياً، لكن التركيبة الدقيقة
بين الترقية وضبط العلم معاً لم تُختبَر بسيناريو مخصَّص واحد). لا يُعدّ عيباً منطقياً — تتبُّع الكود يدوياً
يؤكد صحة السلوك في هذه الحالة — لكنه فجوة تغطية موثَّقة صراحة.

---

## الجولة 165 — سلامة WAL في نسخة الأمان الوقائية وحذف ملفات -wal/-shm القديمة بعد الاستعادة

**الثغرة:** `AppDbContext.ApplyPragmas` يفعِّل `PRAGMA journal_mode = WAL` عند كل إقلاع، فتعمل قاعدة
البيانات الحيّة دائماً بملفَي `Sources.db-wal`/`Sources.db-shm` قد يحملان معاملات مُلتزمة (committed) لم
تُدمَج بعد في الملف الرئيسي `Sources.db`. في `BackupService.RestoreBackup`: (1) نسخة الأمان الوقائية قبل
الاستعادة (`safetyBackupDb`) كانت تُبنى بـ`File.Copy` خام لملف `_dbPath` فقط — بلا أي checkpoint وبلا نسخ
لملفَي `-wal`/`-shm` — فإن وُجدت معاملات مُلتزمة في WAL لحظة الضغط على "استعادة"، تُسقَط بصمت من نسخة
الأمان؛ وهذا الملف نفسه يُستخدَم أيضاً للتراجع (rollback) إن فشل فحص توافق المخطط بعد الاستعادة، فقد يُرجِع
فشل استعادة واحد المستخدمَ إلى حالة أقدم مما كانت عليه لحظة الضغط على الزر. (2) بعد استبدال ملف قاعدة
البيانات الرئيسي بالمحتوى المُستعاد الجديد (مسار ZIP ومسار `.db` القديم كليهما)، ملفَا `Sources.db-wal`/
`Sources.db-shm` القديمان (العائدان لجيل قاعدة البيانات السابق) لم يكونا يُحذَفان أو يُصفَّران إطلاقاً —
خطر خلط/تلف بيانات حقيقي عند إعادة فتح القاعدة لاحقاً (بما فيها فحص التوافق نفسه) إن حاول SQLite تطبيق
إطارات WAL من جيل سابق فوق ملف رئيسي جديد من مصدر مختلف. الدليل على أن هذا سهو لا قرار متعمَّد:
`CreateBackup` في نفس الملف يستخدم فعلاً نمط `PRAGMA .../VACUUM INTO` الآمن تماماً تجاه WAL (ومختبَر فعلاً
بـ`CreateBackup_UsingVacuumInto_ProducesValidAndIdenticalDatabaseWithWalData`) — أي أن الفريق حلّ هذه
المشكلة بعينها في `CreateBackup` ولم يُطبِّقها قط على `RestoreBackup`؛ كذلك `LegacyDatabaseImporter.cs`
يتعامل صراحةً مع ملفات `-wal`/`-shm` الجانبية (`SideFileSuffixes`) في سياق مختلف — النمط الآمن موجود
ومُثبَت في المستودع نفسه، لم يُطبَّق هنا فقط.

**الإصلاح (ثلاثة أجزاء، بلا تغيير في توقيع `RestoreBackup`/`CreateBackup` ولا رسائل النجاح/الفشل ولا منطق
فحص التوافق نفسه):**

1. استُبدل `File.Copy` الخام لبناء `safetyBackupDb` بنفس نمط `PRAGMA journal_mode.../VACUUM INTO` المُستخدَم
   فعلاً في `CreateBackup` (اتصال SQLite بوضع `ReadOnly` على `_dbPath`، `VACUUM INTO '<مسار مُهرَّب>'`) —
   يُنتِج ملف أمان ذاتي الاكتفاء يتضمَّن كل البيانات المُلتزمة حتى لحظة الاستعادة بلا حاجة لنسخ ملفَي
   `-wal`/`-shm` بشكل منفصل. اسم الملف الناتج (`SOURCES_pre_restore_{timestamp}.db`) واستخدامه في مسار
   التراجع لم يتغيَّرا.
2. دالة خاصة جديدة `DeleteStaleWalShmFiles()` تُستدعى فوراً بعد استبدال `_dbPath` بالمحتوى المُستعاد في
   كلا المسارين (ZIP والملف المباشر `.db`)، قبل أي محاولة فتح اتصال جديد لفحص توافق المخطط، وتحذف
   `_dbPath + "-wal"` و`_dbPath + "-shm"` إن وُجدا، بـ`try/catch` منفصل لكل ملف (فشل حذف أحدهما لا يوقف
   بقية الاستعادة، ويُسجَّل تحذير عبر `LoggerService.LogWarning` بنفس أسلوب التحذيرات القائم في الملف نفسه
   لحالات حذف مشابهة).
3. نفس التنظيف يُطبَّق في مسار التراجع (rollback) بعد فشل فحص التوافق، مباشرة بعد نسخ `safetyBackupDb` مرة
   أخرى فوق `_dbPath`، بنفس نمط `try/catch` لكل ملف ونفس أسلوب التحذير.

**اختبارات جديدة (`BackupServiceTests.cs`، 2 اختبارين إضافة صرفة):**
- `RestoreBackup_PreRestoreSafetyCopy_CapturesPendingWalDataViaVacuumInto`: يفتح اتصالاً بوضع WAL على
  `_dbPath` ويُدرِج سجلاً ويُبقي الاتصال مفتوحاً عمداً (بلا إغلاق) كي يبقى السجل في ملف `-wal` بلا دمج في
  الملف الرئيسي، ثم يستدعي `RestoreBackup` بنسخة احتياطية متوافقة أخرى، ثم يفتح ملف `SOURCES_pre_restore`
  الناتج مباشرة ويتحقق أنه يحتوي فعلاً على السجل الذي كان معلَّقاً في WAL لحظة الاستدعاء.
- `RestoreBackup_SuccessfulRestore_DeletesStaleWalAndShmFilesFromPreviousGeneration`: يُنشئ `_dbPath`
  بوضع WAL بحيث يترك ملفَي `-wal`/`-shm` فعليين على القرص (اتصال يُغلَق بعد الإدراج مباشرة بلا
  `wal_checkpoint` صريح، تاركاً الملفين حتى تفريغ تجمُّع اتصالات SQLite)، ثم يستدعي `RestoreBackup` بنجاح،
  ثم يتحقق أن `File.Exists` كليهما `false` بعد ذلك. **ملاحظة تصميمية:** نسخة الاستعادة الهدف في هذا
  الاختبار بُنيَت عمداً بلا `PRAGMA journal_mode=WAL` (خلافاً لبقية استخدامات `CreateValidSqliteDatabase`
  في نفس الملف) — لأن تفعيل WAL على الملف المُستعاد نفسه يجعل فحص توافق المخطط اللاحق (غير المُعدَّل في
  هذه الجولة) يُعيد إنشاء ملفَي `-wal`/`-shm` شرعياً وحتمياً بمجرد إعادة فتح القاعدة للقراءة — وهو سلوك
  SQLite متوقَّع تماماً وغير متعلق بالثغرة قيد الاختبار، فكان سيُلبِس الاختبار بنتيجة إيجابية كاذبة الفشل.
  هذا العزل يجعل الاختبار يتحقق بدقة من أن الملفين "القديمين" العائدين لجيل قاعدة البيانات السابق قد حُذفا
  فعلاً بواسطة `DeleteStaleWalShmFiles`.

**اختبار ثالث مطلوب في العقد — انحراف موثَّق:** محاولة محاكاة قفل حقيقي لملف `-wal`/`-shm` من نفس العملية
عبر اتصال SQLite مفتوح تبيَّن تجريبياً أنها تمنع فعلاً عملية `File.Delete` (رسالة Windows "The process
cannot access the file because it is being used by another process")، لكنها أيضاً تمنع الاستعادة بأكملها
من إعادة فتح الملف الرئيسي بشكل موثوق عبر بيئات تشغيل مختلفة (محلي مقابل CI)، ومحاكاة قفل من عملية خارجية
منفصلة تتطلب عملية فرعية منفصلة وتعقيداً غير متناسب يجعل الاختبار هشاً (flaky). بدلاً من فرض اختبار هش، تم
التحقق يدوياً بمراجعة الكود من أن `DeleteStaleWalShmFiles` تستخدم `try/catch` منفصلاً تماماً لكل ملف مع
`LoggerService.LogWarning` بلا إعادة رمي أي استثناء — بما يطابق نمط `try/catch` القائم مسبقاً في نفس الملف
لحالات حذف مشابهة (مثال: حذف `tempExtractedDb`، حذف ملفات `Certificates`) — فلا يمكن لفشل حذف ملف واحد أن
يُسقط عملية الاستعادة كاملة.

### النتائج

2 اختبار جديد بالإضافة الصرفة بلا أي حذف أو تعديل لسلوك الاختبارات الثلاثة عشر الأخرى القائمة في
`BackupServiceTests.cs` (بما فيها اختبارات `RestoreBackup` الثلاثة القائمة للتوافق/الرفض بلا
`__EFMigrationsHistory`/الرفض بترحيل مستقبلي مجهول، واختبار إنشاء نسخة الأمان القائم — جميعها لا تزال تمر
دون أي تعديل على نيّتها أو تأكيداتها، فقط تُمارِس الآن مسار `VACUUM INTO` الجديد داخلياً بدل `File.Copy`).
37/37 اختباراً في `BackupServiceTests.cs` بنجاح، و1276/1276 اختباراً في كامل حزمة الاختبار (Debug محلياً)،
صفر فشل، صفر تجاوز. بناء `Release`/`Debug` بصفر أخطاء وخمسة تحذيرات `CS8604` سابقة الوجود بالضبط في
`LoginWindow.xaml.cs` (سطرا 104 و199) و`ViewInstantiationTests.cs` (سطر 218)، بلا أي علاقة بهذه الجولة
(ملفات غير مُلمَسة في هذا الـdiff). لا migration جديدة ولا أي تغيير في مخطط قاعدة البيانات في هذه الجولة —
الإصلاح بالكامل داخل طبقة الخدمة فقط. لم تُلمس `LoginWindow`/`LoginView`/`SplashWindow` ولا
`AuthorizationGuard.RequireActivated` ولا مجلد الشهادات أو منطقه.

### الانحرافات عن العقد الأصلي (موثَّقة صراحة، بلا تجميل)

1. **الاختبار الثالث المطلوب (محاكاة فشل حذف ملف مقفول):** لم يُنفَّذ كاختبار آلي فعلي — تم توثيقه أعلاه
   كانحراف صريح بدل فرض اختبار هش، مع تحقُّق يدوي بمراجعة الكود من أن السلوك المطلوب (فشل حذف أحد الملفين
   لا يُسقط الاستعادة) محقَّق فعلاً عبر بنية `try/catch` المنفصلة لكل ملف.
2. **بناء اختبار نسخة الهدف في الاختبار الثاني بلا `PRAGMA journal_mode=WAL`:** انحراف تصميمي بسيط عن
   الوصف الحرفي للعقد ("keep a connection open during insert, as in test 1")، مبرَّر أعلاه بالتفصيل — بلا
   هذا العزل، يُعيد فحص توافق المخطط (غير المُعدَّل) إنشاء ملفَي `-wal`/`-shm` شرعياً من الجيل الجديد فور
   إعادة فتح القاعدة، فيُفسِد الاختبار بلا علاقة بالثغرة قيد الاختبار.

---

## الجولة 166 — إعادة حساب النشاط الحالي بشكل حيّ داخل `SourceService.GetAllSources`

**الثغرة:** `SourceService.GetSourceById` (الأسطر 47-69) تفحص إن كانت حالة المصدر `InUse`/`Storage`،
وفي هذه الحالة تبني `isotopesDict`/`unitsDict` من `db.Radioisotopes`/`db.ActivityUnits` وتستدعي الدالة
الخاصة `CalculateSourceCurrentActivityInMemory` لإعادة حساب `CurrentActivityValue` حياً من الانحلال في
الذاكرة (بلا `SaveChanges`، قراءة `AsNoTracking`) في كل استدعاء. أما `GetAllSources` (الأسطر 28-45) —
تجلب القائمة الكاملة بنفس `Include`/`AsNoTracking`/`AsSplitQuery`، لكنها **لا تستدعي أي إعادة حساب
إطلاقاً** وتُعيد `CurrentActivityValue` كما هو مخزَّن. `GetLowActivitySources` (الأسطر 576-590) تستدعي
`GetAllSources` داخلياً فترث نفس العيب. مستهلكو `GetAllSources`: `SourcesViewModel` (القائمة الرئيسية)،
`DashboardViewModel`، `ReportsViewModel` (بما فيها تقرير المصادر منخفضة النشاط) — فقد تعرض شاشة قائمة
المصادر والتقارير قيمة راكدة تختلف عمّا تعرضه شاشة تفاصيل المصدر نفسه في نفس اللحظة. `UpdateAllCurrentActivities()`
(الأسطر 462-482) تنفيذ صحيح تماماً لدفعة إعادة حساب وحفظ كاملة، لكن `grep` شامل عبر المستودع كله أكَّد
أن مستدعييها الوحيدين اختباران في `SourceServiceTests.cs` — لا `App.xaml.cs`، لا مؤقِّت، لا ViewModel.

**القرار المعماري:** تطبيق نفس نمط `GetSourceById` (إعادة حساب في الذاكرة بلا حفظ) داخل `GetAllSources`
مباشرة بعد تجسيد الاستعلام، بدل استدعاء `UpdateAllCurrentActivities()` من مسار إقلاع/مؤقِّت — يحافظ هذا
على مسار القراءة صرفاً (`AsNoTracking`، بلا أي أثر جانبي مُحفَّظ) ويضمن الصحة عند كل قراءة بصرف النظر عن
تشغيل أي مهمة دفعية سابقاً. `UpdateAllCurrentActivities()` تصبح زائدة فعلياً بعد هذا الإصلاح لكنها لم
تُحذف بقرار صريح — حذفها مؤجَّل لجولة تنظيف مستقبلية بموافقة إدريس.

**التنفيذ:** داخل `GetAllSources` حصراً، بعد `.ToList().DistinctBy(s => s.Id).ToList()` الحالية حرفياً
بلا أي تعديل عليها، بناء `isotopesDict`/`unitsDict` مرة واحدة (لا لكل مصدر) من
`db.Radioisotopes.AsNoTracking().ToDictionary(r => r.Id)`/`db.ActivityUnits.AsNoTracking().ToDictionary(u => u.Id)`
(نفس نمط `GetSourceById`)، ثم لكل مصدر في القائمة الناتجة بحالة `InUse`/`Storage` استدعاء
`CalculateSourceCurrentActivityInMemory` الخاصة القائمة دون أي تعديل على توقيعها أو منطقها. لم تُلمس
`GetSourceById`/`CreateSource`/`UpdateSource`/`RestoreSource`/`UpdateAllCurrentActivities`/
`CalculateSourceCurrentActivityInMemory` بأي تعديل.

**اختبارات جديدة (`SourceServiceTests.cs`، 3 اختبارات إضافة صرفة):**
- `GetAllSources_InUseSourceWithStaleStoredActivity_RecalculatesLiveDecayedValue`: مصدر Co-60 (نصف
  العمر 5.27 سنة) بمعايرة قبل 5 سنوات وقيمة `CurrentActivityValue` راكدة مضبوطة يدوياً (9999.0)؛ يثبت
  أن `GetAllSources` تُعيد قيمة أقل من 6000 فعلياً (الانحلال الحقيقي) لا القيمة الراكدة.
- `GetAllSources_WasteAndTransferSources_AreReturnedUnchangedWithoutRecalculation`: يثبت أن مصادر
  `Waste`/`Transfer` تُعاد بلا أي تعديل على قيمتها المخزَّنة، مطابقاً استثناء `GetSourceById`/
  `UpdateAllCurrentActivities` القائم.
- `GetLowActivitySources_StaleStoredValueAboveThreshold_TrueDecayedValueBelowThreshold_IsIncluded`:
  يثبت الفائدة المباشرة على `GetLowActivitySources` دون أي لمس مباشر لدالتها — مصدر بقيمة مخزَّنة فوق
  العتبة (60%) لكن قيمته الحقيقية بعد الانحلال (Co-60 بعد 20 سنة، ~7.2%) تحت العتبة (10%)، يُدرَج الآن
  ضمن النتائج بينما لم يكن يُدرَج قبل التصحيح.

### النتائج

3 اختبارات جديدة بالإضافة الصرفة في `SourceServiceTests.cs`. 1279/1279 اختباراً في كامل حزمة الاختبار
(Debug محلياً، +3 عن الجولة 165)، صفر فشل، صفر تجاوز. بناء بصفر أخطاء وثلاثة تحذيرات `CS8604` سابقة
الوجود بالضبط في `LoginWindow.xaml.cs` (سطرا 104 و199) و`ViewInstantiationTests.cs` (سطر 218)، بلا أي
علاقة بهذه الجولة (ملفات غير مُلمَسة في هذا الـdiff). لا migration جديدة ولا أي تغيير في مخطط قاعدة
البيانات — الإصلاح بالكامل داخل طبقة الخدمة فقط. لم تُلمس `LoginWindow`/`LoginView`/`SplashWindow`.

### الانحرافات عن العقد الأصلي (موثَّقة صراحة، بلا تجميل)

1. **`GetLowActivitySources_ReturnsOnlySourcesAtOrBelowThreshold` في `SourceServiceTests.cs`
   (داخل قائمة الملفات المسموحة):** كان يضبط `CurrentActivityValue` يدوياً مباشرة (80.0 و500.0) مع
   `calibrationDate: DateTime.Now` (بلا انحلال فعلي تقريباً). بعد التصحيح، `GetAllSources` تُعيد حساب
   القيمة الحقيقية من الانحلال فتتجاهل الضبط اليدوي تماماً، فتصبح النسبتان ~100% لكلا المصدرين وتفشل
   التأكيدات القديمة. عولج بتغيير تاريخ المعايرة فقط (بلا أي قيمة مخزَّنة يدوياً بعد الآن) إلى فترات
   تُنتِج نسبة انحلال حقيقية مطابقة لنيّة الاختبار الأصلية تماماً (Cs-137، نصف العمر 30.08 سنة،
   النسبة = 0.5^(t/T)): -120 سنة للمصدر المنخفض (~6.3%)، -10 سنوات للمصدر المرتفع (~79%). لم يتغيّر
   عدد أو نيّة أي تأكيد.
2. **`GetLowActivitySources_FiltersAccuratelyAroundThreshold` في `Sources.Tests/SourceRepositoryTests.cs`
   (خارج قائمة الملفات المسموحة في نص العقد — لم يُذكر هذا الملف صراحة):** نفس فئة العطل تماماً — خمسة
   مصادر بقيم `CurrentActivityValue` مضبوطة يدوياً مباشرة بمعايرة افتراضية (قبل 30 يوماً فقط، انحلال
   ضئيل جداً لـCs-137)، فتفشل جميع التأكيدات الخمسة بعد التصحيح لأن كل القيم الحقيقية تعود قريبة جداً
   من 100%. اعتُبر تعديل هذا الملف ضرورياً (لا اختياريـاً) لتحقيق بند العقد الرقم 4 الذي يُلزم بنجاح
   **كل** اختبارات `GetAllSources`/`GetSourceById`/`GetLowActivitySources` القائمة في المستودع، لا فقط
   تلك الموجودة في `SourceServiceTests.cs` حصراً. عولج بتغيير تاريخ المعايرة فقط لكل من المصادر الأربعة
   غير الملغاة بحالتها (`Waste` غير متأثر بالتصحيح فتُرك بلا تعديل) إلى فترات تُنتِج نسبة الانحلال
   الحقيقية المقصودة بدقة لكل حالة (~5.0%، ~9.98%، ~11.2%، ~50.2% لكلٍّ من `SRC-LOW-5`/`SRC-LOW-10`/
   `SRC-LOW-10-PLUS`/`SRC-LOW-50` على التوالي)، بلا حذف أو تخفيف أي تأكيد وبلا تغيير نيّة الاختبار.

---

## الجولة 167 — مثبِّت Windows حقيقي عبر Inno Setup (Setup.exe) (ب8، جزء أول)

**النطاق:** أول جزء تنفيذي فعلي من موانع النشر ب8 (نظام النشر) — إنتاج سكربتات
بناء المثبِّت نفسها، لا تعديل أي منطق تشغيلي في التطبيق. أربعة ملفات جديدة تحت
`deploy\` وملف توثيق جديد `docs\deployment-guide.md`، بالإضافة إلى ست خصائص
تجميعة (`AssemblyProduct`/`AssemblyCompany`/`AssemblyCopyright`/
`AssemblyVersion`/`AssemblyFileVersion`/`AssemblyInformationalVersion`) أُضيفت
إلى `Sources-System-Project\AssemblyInfo.cs` الذي كان يحتوي `ThemeInfo` فقط
(بقي بلا تعديل).

**الملفات الجديدة:**
- `deploy\installer.iss`: سكربت Inno Setup 6. `AppId` ثابت
  `{{DF8A9B0D-5D38-4E06-9EB7-D7593FAF3B77}`، `AppMutex` مطابق حرفياً لسلسلة
  `App.xaml.cs:47` (`{Sources-RST-2026-UNIQUE-MUTEX}`)، عربي عبر
  `compiler:Languages\Arabic.isl`، `PrivilegesRequired=admin`،
  `ArchitecturesInstallIn64BitMode=x64compatible`. `[Files]` ينسخ محتوى مجلد
  النشر فقط إلى `{app}` — لا مرجع إطلاقاً لـ `%LocalAppData%\Sources` أو
  `%ProgramData%\Sources`. لا `[UninstallDelete]` عمداً (موثَّق بتعليق في رأس
  الملف) كي لا يلمس إلغاء التثبيت بيانات المستخدم الحيّة. لا توقيع كود.
- `deploy\assets\generate-wizard-images.ps1`: يحوّل شعارات `Assets\*.png`
  القائمة إلى BMP بمقاسات معالج Inno Setup (164×314 كبيرة، 55×58 صغيرة، وثلاث
  90×90 لصفحة اعتمادات محتملة مستقبلاً) عبر `System.Drawing` فقط، بهامش 10%
  وتحجيم `HighQualityBicubic`. مخرجات بناء، لا تُحفَظ في نظام التحكم بالإصدار.
- `deploy\build-installer.ps1`: ينسّق `dotnet publish` (ذاتي الاكتفاء win-x64،
  `PublishSingleFile=false` دائماً — صراحة لا `true`)، ثم توليد صور المعالج، ثم
  استدعاء `ISCC.exe` (افتراضياً `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`،
  قابل للتجاوز بمعامل) على `installer.iss`. يرمي استثناءً بنص عربي واضح عند
  فشل أي خطوة أو غياب `ISCC.exe`.
- `docs\deployment-guide.md`: شرح تشغيل `build-installer.ps1`، اشتراط تثبيت
  Inno Setup 6 يدوياً على جهاز البناء، الخطوات الخمس الإلزامية للتحقق اليدوي
  (تثبيت نظيف، إدخال بيانات حقيقية، إعادة تشغيل Setup.exe كترقية، إلغاء
  التثبيت عبر لوحة التحكم، إعادة التثبيت والتأكد من التقاط البيانات القديمة)،
  وتوثيق صريح أن `%LocalAppData%\Sources` و `%ProgramData%\Sources` خارج نطاق
  المثبِّت بالكامل.

**قرار معماري — بديل صفحة الاعتمادات المبسَّط:** عقد الجولة يسمح صراحة بصفحة
اعتمادات مخصَّصة متعددة الشعارات (`TBitmapImage` عبر نمط
`dontcopy`+`ExtractTemporaryFile`) **أو** ببديل مبسَّط موثَّق إن تعذّر التحقق من
عمل النمط الأول فعلياً بـ ISCC. بيئة تنفيذ هذه الجولة تحتوي `ISCC.exe` مثبَّتاً
فعلياً على المسار القياسي، لكن أداة الصدفة المعزولة في هذه الجلسة ترفض تنفيذ
أي ملف تنفيذي خارج `git`/`dotnet` (`powershell.exe`، `ISCC.exe`، `cmd.exe`
جميعها رُفضت بحاجز عزل الشجرة العاملة) — فتعذّر تشغيل سكربت توليد الصور
وتصريف الـ.iss فعلياً للتحقق من نمط Pascal Script قبل الاعتماد عليه في سكربت
إنتاجي. طُبِّق **البديل المبسَّط المُصرَّح به صراحة في العقد**: فتحتا صورة
المعالج القياسيتان في Inno Setup (`WizardImageFile`/`WizardSmallImageFile`)
بدل صفحة اعتمادات مخصَّصة، بلا أي ملف اعتمادات نصي أو صوري إضافي غير مذكور في
قائمة الملفات المسموحة بالعقد (لم يُضَف أي ملف `credits.txt` أو ما شابه لتفادي
توسيع النطاق).

### النتائج

`dotnet build` (Debug) نجح بصفر أخطاء بعد إضافة خصائص `AssemblyInfo.cs`
الستة — 4 تحذيرات `CS8604` مطابقة تماماً للتحذيرات السابقة الموثَّقة في
`LoginWindow.xaml.cs` (سطرا 104 و199، مكرران بسبب بناء `Sources.csproj` ونسخة
`wpftmp` مؤقتة)، بلا أي تحذير جديد. لا `dotnet test` لهذه الجولة (لا منطق C#
تشغيلي تغيَّر خارج خصائص التجميعة الوصفية). لم يُشغَّل `ISCC.exe` فعلياً لعدم
توفر تنفيذ الملفات التنفيذية خارج git/dotnet في بيئة هذه الجلسة — البناء
الفعلي لـ `SourcesSystemSetup.exe` والتحقق البصري الخماسي في
`docs\deployment-guide.md` §4 يبقيان مهمة إدريس على جهاز Windows حقيقي، كما هو
منصوص عليه صراحة في عقد الجولة. لم تُلمس `LoginWindow`/`LoginView`/
`SplashWindow`. لا ترحيل مخطط (لا تغيير في قاعدة البيانات إطلاقاً في هذه
الجولة).

### الانحرافات عن العقد الأصلي (موثَّقة صراحة)

1. **بديل صفحة الاعتمادات المبسَّط استُخدم فعلاً (لا مجرد احتياط نظري):** كما
   هو موضَّح أعلاه، تعذّر تشغيل `ISCC.exe` في بيئة التنفيذ لحاجز عزل الصدفة
   (وليس لغياب Inno Setup 6 عن الجهاز فعلياً — هو مثبَّت). هذا الانحراف
   مُصرَّح به صراحة في نص العقد كبديل مقبول عند تعذّر التحقق، وليس خروجاً غير
   مصرَّح به.
2. **لا شيء آخر خارج ما سبق.** لم تُبنَ أي صورة BMP أو أي ملف مثبِّت فعلي
   وتُدرَج في المستودع؛ الملفات الستة المذكورة في العقد فقط هي ما جرى تعديله
   أو إنشاؤه.

ب8 يبقى **☐ غير مكتمل** — هذه الجولة تنتج سكربتات البناء المصدرية فقط؛
البناء الفعلي والتحقق البصري الخماسي والتوقيع (إن قُرِّر لاحقاً) لا تزال
خطوات لاحقة مطلوبة قبل اعتبار ب8 مغلقاً.
